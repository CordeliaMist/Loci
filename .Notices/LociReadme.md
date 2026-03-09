# NOTICE:
The Loci segment of Sundouleia is derrived from Moodles, licensed under:
***This project is not affiliated with, endorsed, or supported by Moodles.***
```
BSD 3-Clause License
Copyright (c) 2024, Kane Valentine

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its
   contributors may be used to endorse or promote products derived from
   this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```
## Relevance
Loci provides **public API endpoints** and extends existing functionality.  
Its goal is to offer accessible control over custom statuses owned by the client, and other users.  

Key features include:  
- Lock/unlock client statuses  
- Register/unregister monitored actors for identified status management
- Apply statuses/presets by **Tuple** and **ID**
- Can be used/interfaced with **without a Sundouleia account / connection**, allowing any plugin to use it as an IPC endpoint

Originally designed as a placeholder, Loci now has grown to expose several new IPC endpoints, its own locking system, and polished target application system.  
> There is no exclusivity with Loci — it is designed for those who want full control over custom statuses.

## Summary
Loci is a safe, transformative, derivative work with optimized functionality and additional features:  
- **50+ IPC endpoints** for external plugin use  
- Inclusive Target Application system  
- Inclusive custom status locking system (safely clears locks on shutdown)  
- Detailed ImGui supported display of color formatted statuses and descriptions
- Planned integrate for Minion/Pet status support