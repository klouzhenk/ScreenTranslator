# Ізоляція процесів і IPC

## Чому процеси не бачать змінні одне одного
Кожен процес має власний **адресний простір** (virtual memory). Коли процес A пише `int x = 5`, то в процесі B за *тією самою* адресою лежить щось зовсім інше, або нічого. OS гарантує цю ізоляцію — інакше будь-який процес читав би й ламав память іншого.

Наслідки для C#:

**Статична булева не працює між процесами:**
```csharp
private static bool _isRunning;
```
Другий екземпляр стартує з чистою копією класу — для нього `_isRunning == false`, бо це *його* змінна в *його* памʼяті. Статика статична в межах процесу, не OS.

**`lock` теж не працює:**
```csharp
private static readonly object _gate = new();
lock (_gate) { ... }
```
`lock` під капотом — `Monitor.Enter` по sync block index обʼєкта `_gate`. Але `_gate` — managed-обʼєкт на heap *цього* процесу. У другому процесі свій `_gate`, свій heap, свій Monitor. Не знають одне про одного.

## Що МОЖНА ділити між процесами
Усе, що живе **не в памʼяті процесу**, а керується **OS або файловою системою**:

| Механізм | Де живе | Коли брати |
|---|---|---|
| **Named mutex / semaphore / event** | Kernel object namespace | Single-instance, простий сигнал «я тут» |
| **Lock-файл** | Файлова система | Якщо вже треба писати щось на диск (PID-файл демона) |
| **Named pipe** | OS IPC | Треба передавати повідомлення між процесами |
| **Shared memory (memory-mapped file)** | OS virtual memory map | Треба ділити великі обʼєми даних |
| **Registry key** | Windows Registry | Рідко, bloat |
| **Socket (localhost або TCP)** | OS network stack | Процеси можуть бути на різних машинах |

## «Та сама машина» — не завжди очевидно
Коли кажуть «дві машини» в контексті IPC — часто мають на увазі не фізичні, а **логічні**: різні kernel namespace'и, різні ізольовані середовища. Два процеси на одному залізі можуть бути «різними машинами», якщо вони під різними kernel'ами.

Приклади «різних машин» в одній коробці:
- **VM-и** (Hyper-V, VMware, VirtualBox).
- **WSL2** — див. нижче, найпоширеніший побутовий приклад.
- **Контейнери** (Docker, Podman) — спільний kernel, але окремі namespace'и для PID/мережі/FS; kernel objects теж не бачать.
- **Android-емулятор у Windows**.

Правило:
- **Один OS kernel** → можна named mutex / shared memory / pipes.
- **Різні OS kernels** (навіть на одному залізі!) → тільки мережа або спільний диск.

## WSL2 як канонічний приклад

**WSL1** — Linux-подібний шар поверх Windows kernel. Спільний kernel. Microsoft його майже закинув.

**WSL2** (default) — повноцінна Linux VM під Hyper-V:
- Свій Linux kernel.
- Своя RAM (виділена з Windows).
- Свій мережевий стек.
- Своя файлова система (`ext4` всередині `.vhdx`).

Для Windows-процесу WSL2 — буквально окрема машина.

**Що НЕ ділиться:**
- Kernel objects (named mutex/semaphore/event) — Windows-specific, Linux їх не бачить.
- Процеси і сигнали — Linux не шле `SIGTERM` Windows-процесу.
- Pipes і shared memory напряму — Windows named pipe ≠ Unix domain socket.
- Managed heap, `lock`, `bool` — різні рантайми взагалі.

**Що МОЖНА:**
- **Мережа.** WSL2 має автоматичний localhost forwarding: сервер на Windows `localhost:5000` доступний з WSL як `localhost:5000`.
- **Файлова система.** З WSL видно Windows через `/mnt/c/...`, з Windows видно WSL через `\\wsl$\Ubuntu\...`.

**Практичний приклад.** Windows-застосунок + локальна LLM в WSL через `ollama serve` на порту 11434:
```csharp
var http = new HttpClient();
var response = await http.PostAsync("http://localhost:11434/api/generate", ...);
```
Жодного named mutex тут не вийде — тільки HTTP/TCP, як до віддаленої машини.

## WSL2 networking modes
У `.wslconfig` (файл у `%USERPROFILE%` на Windows-стороні) можна перемкнути режим мережі:

```ini
[wsl2]
networkingMode=mirrored
```

- **`nat`** (default) — WSL2 у своїй підмережі (`172.x.x.x`), Windows робить NAT. Автоматичний localhost-forwarding для TCP.
- **`mirrored`** — WSL2 ділить мережеві інтерфейси з Windows. `localhost` буквально той самий. Доступний з WSL 2.0.0+ на Win11 22H2+. Плюси: IPv6, multicast, нормальна робота з корпоративним VPN. Мінуси: деякі конфіги Docker/libvirt всередині WSL вередують.

Перевірити режим зсередини WSL: `ip addr show eth0` — якщо IP збігається з Windows-інтерфейсом (той самий, що в `ipconfig`), то `mirrored`; якщо `172.x.x.x` — `nat`.

## Mental model
- **`lock` / `bool` / `Interlocked` / анонімний `Mutex`** — синхронізація **потоків** (shared memory of one process).
- **Named `Mutex` / `Semaphore` / `Event`** — синхронізація **процесів у межах одного Windows kernel**.
- **Сокети / спільний файл** — єдиний варіант між *різними* kernel'ами (WSL ↔ Windows, VM ↔ host, container ↔ host).

Named mutex виглядає як та сама абстракція, що й анонімний, але під капотом у named-варіанта OS kernel object — тому саме він єдиний з сімейства `Mutex/Semaphore/Event`, здатен перетнути межу процесу (але не межу kernel'a).
