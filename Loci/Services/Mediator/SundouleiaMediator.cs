using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Reflection;

namespace Loci.Services.Mediator;

public sealed class LociMediator : IHostedService
{
    private readonly object _addRemoveLock = new();
    private readonly ConcurrentDictionary<object, DateTime> _lastErrorTime = [];
    private readonly ILogger<LociMediator> _logger;
    private readonly CancellationTokenSource _loopCts = new();
    private readonly ConcurrentQueue<MessageBase> _messageQueue = new();
    private readonly ConcurrentDictionary<Type, HashSet<SubscriberAction>> _subscriberDict = [];
    private bool _processQueue = false;
    private readonly ConcurrentDictionary<Type, MethodInfo?> _genericExecuteMethods = new();
    public LociMediator(ILogger<LociMediator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Outputs all current active mediator subscribers. Useful for debugging if we 
    ///     ever wanted to attach it to a commend manager or something.
    /// </summary>
    public void PrintSubscriberInfo()
    {
        // for each subscriber in the subscriber dictionary, log the subscriber and the messages they are subscribed to
        var subscribbers = _subscriberDict
            .SelectMany(c => c.Value.Select(v => v.Subscriber))
            .DistinctBy(p => p)
            .OrderBy(p => p.GetType().FullName, StringComparer.Ordinal)
            .ToList();

        foreach (var subscriber in subscribbers)
        {
            // log the subscriber
            _logger.LogInformation($"Subscriber {subscriber.GetType().Name}: {subscriber}");
            // create a string builder
            StringBuilder sb = new();
            sb.Append("=> ");
            // for each item in the subscriber dictionary, if the subscriber is the same as the subscriber in the loop, append the name of the message to the string builder
            foreach (var item in _subscriberDict.Where(item => item.Value.Any(v => v.Subscriber == subscriber)).ToList())
                sb.Append(item.Key.Name).Append(", ");

            // if the string builder is not equal to "=> ", log the string builder
            if (!string.Equals(sb.ToString(), "=> ", StringComparison.Ordinal))
                _logger.LogInformation("{sb}", sb.ToString());

            _logger.LogInformation("---");
        }
    }

    /// <summary>
    ///     Allows publishing a message of type T. <para />
    ///     If the message indicates it should keep the thread context, it's executed immediately.
    ///     Otherwise, it's enqueued for later processing.
    /// </summary>
    public void Publish<T>(T message) where T : MessageBase
    {
        // if the message should keep the thread context, execute the message so it executes immediately
        if (message.KeepThreadContext)
            ExecuteMessage(message);
        // otherwise, enqueue the message for later processing (potentially in a different thread)
        else
            _messageQueue.Enqueue(message);
    }

    /// <summary>
    ///     The required startAsync method by the mediator-base <para />
    ///     Begins processing the message queue in a loop
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            while (!_loopCts.Token.IsCancellationRequested)
            {
                // while we should not be processing the queue, delay for 100ms then try again,
                // and keep doing this until we should process the queue
                while (!_processQueue)
                    await Task.Delay(100, _loopCts.Token).ConfigureAwait(false);

                // await 100 ms before processing the queue
                await Task.Delay(100, _loopCts.Token).ConfigureAwait(false);

                // while the message queue tries to dequeue a message, execute the message
                HashSet<MessageBase> processedMessages = [];
                while (_messageQueue.TryDequeue(out var message))
                {
                    // Skip processed messages so we dont execute them more than once.
                    if (processedMessages.Contains(message))
                        continue;

                    processedMessages.Add(message);
                    ExecuteMessage(message);
                }
            }
        });

        _logger.LogInformation("Started Loci Mediator");
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Ensure the mediators message queue is cleared upon it stopping,
    ///     and that we prevent the loop from continuing to run after plugin shutdown.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _messageQueue.Clear();
        _loopCts.Cancel();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Subscribe an <paramref name="action"/> to the <paramref name="subscriber"/> for a <seealso cref="MessageBase"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public void Subscribe<T>(IMediatorSubscriber subscriber, Action<T> action) where T : MessageBase
    {
        // lock the add remove lock so it becomes thread safe
        lock (_addRemoveLock)
        {
            // if the subscriber dictionary does not contain the type of T, add it to the subscriber dictionary
            _subscriberDict.TryAdd(typeof(T), []);

            // if we are already subscribed to this message, throw an exception
            if (!_subscriberDict[typeof(T)].Add(new(subscriber, action)))
                throw new InvalidOperationException("Already subscribed");

            // otherwise, we would have sucessfully added it to the dictionary, logging its sucess afterward
            _logger.LogDebug("Subscriber added for message "+typeof(T).Name+": "+subscriber.GetType().Name, LoggerType.Mediator);
        }
    }

    /// <summary>
    ///     Unsubscribe from a subscribed mediator message in 
    ///     <paramref name="subscriber"/> matching <see cref="Type"/>
    /// </summary>
    public void Unsubscribe<T>(IMediatorSubscriber subscriber) where T : MessageBase
    {
        // lock the add remove lock so it becomes thread safe
        lock (_addRemoveLock)
        {
            if (_subscriberDict.ContainsKey(typeof(T)))
                _subscriberDict[typeof(T)].RemoveWhere(p => p.Subscriber == subscriber);
        }
    }

    /// <summary>
    ///     Unsubscribe all subscribed MediatorMessages for the <paramref name="mediatorSubscriber"/>'s
    /// </summary>
    internal void UnsubscribeAll(IMediatorSubscriber mediatorSubscriber)
    {
        // lock the add remove lock so it becomes thread safe
        lock (_addRemoveLock)
        {
            // for each key value pair in the subscriber dictionary, remove the subscriber from the dictionary
            foreach (Type kvp in _subscriberDict.Select(k => k.Key))
            {
                int unSubbed = _subscriberDict[kvp]?.RemoveWhere(p => p.Subscriber == mediatorSubscriber) ?? 0;
                if (unSubbed > 0)
                    _logger.LogDebug(mediatorSubscriber.GetType().Name+" unsubscribed from "+kvp.Name, LoggerType.Mediator);
            }
        }
    }

    /// <summary>
    ///     Executes a Mediator subscribed message passed in via reflection and action.
    /// </summary>
    private void ExecuteMessage(MessageBase message)
    {
        // if the subscriber dictionary does not contain the type of the message, return
        if (!_subscriberDict.TryGetValue(message.GetType(), out HashSet<SubscriberAction>? subscribers) || subscribers == null || !subscribers.Any()) 
            return;

        // otherwise, get the subscribers and create a copy of the subscribers
        List<SubscriberAction> subscribersCopy = [];

        // lock the add remove when making the copy to ensure thread safety.
        lock (_addRemoveLock)
            subscribersCopy = subscribers?.Where(s => s.Subscriber != null).ToList() ?? [];

        // Use reflection to collect the information necessary so we can invoke the subscribed action.
        var msgType = message.GetType();
        if (!_genericExecuteMethods.TryGetValue(msgType, out var methodInfo))
        {
            // get the method info for the message type
            _genericExecuteMethods[msgType] = methodInfo = GetType()
                 .GetMethod(nameof(ExecuteReflected), BindingFlags.NonPublic | BindingFlags.Instance)?
                 .MakeGenericMethod(msgType);
        }

        // now that we have it, we can invoke the subscribers actions
        methodInfo!.Invoke(this, [subscribersCopy, message]);
    }

    /// <summary>
    ///     Uses reflection to invoke subscriber actions with the correct message type.
    ///     This method is called by ExecuteMessage().
    /// </summary>
    private void ExecuteReflected<T>(List<SubscriberAction> subscribers, T message) where T : MessageBase
    {
        foreach (SubscriberAction subscriber in subscribers)
        {
            try
            {
                ((Action<T>)subscriber.Action).Invoke(message);
            }
            catch (Bagagwa ex)
            {
                if (_lastErrorTime.TryGetValue(subscriber, out var lastErrorTime) && lastErrorTime.Add(TimeSpan.FromSeconds(10)) > DateTime.UtcNow)
                    continue;

                _logger.LogError(ex.InnerException ?? ex, "Error executing {type} for subscriber {subscriber}",
                    message.GetType().Name, subscriber.Subscriber.GetType().Name);
                _lastErrorTime[subscriber] = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    ///     Starts the message queue processing.
    /// </summary>
    public void StartQueueProcessing()
    {
        _logger.LogInformation("Starting Message Queue Processing");
        _processQueue = true;
    }

    /// <summary>
    ///     A sealed class that stores the Mediator subscriber, caching the action 
    ///     to be executed when the message is published.
    /// </summary>
    private sealed class SubscriberAction
    {
        public SubscriberAction(IMediatorSubscriber subscriber, object action)
        {
            // and stores it to the variables
            Subscriber = subscriber;
            Action = action;
        }

        // the action that should be executed, and the subscriber that should execute it
        public object Action { get; }
        public IMediatorSubscriber Subscriber { get; }
    }
}

