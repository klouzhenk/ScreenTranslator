# Нотатки: .NET

## TargetFramework `net10.0-windows10.0.19041.0`
Складений TFM з трьох частин:
- `net10.0` — рантайм .NET 10.
- `-windows` — Windows-специфічне API: WPF, WinForms, Win32 interop.
- `10.0.19041.0` — мінімальна версія Windows SDK, яка відкриває **WinRT** (Windows Runtime) з .NET.

Без суфікса `10.0.19041.0` неймспейси `Windows.*` (наприклад `Windows.Media.Ocr`) недоступні — компілятор їх не бачить, бо не генерується C# interop для WinRT (via [CsWinRT](https://github.com/microsoft/CsWinRT)).

Число `19041` відповідає Windows 10 версія 2004 (травень 2020) — стабільний мінімум для сучасного WinRT. Бувають вищі (`22621`, `26100`), але для `Windows.Media.Ocr` достатньо.

Побічний ефект: тулінг виставить `<SupportedOSPlatformVersion>` = 10.0.19041.0, компілятор буде лаятись при зверненні до API з новіших Windows.

## WinRT (Windows Runtime) — третій API-світ Windows
Три паралельні платформи API в Windows, які співіснують:

| Коли | API | Що це |
|---|---|---|
| 1985+ | **Win32** | Старі C-функції (`CreateWindow`, `BitBlt`, `RegisterHotKey`). Викликається з C# через P/Invoke. |
| 2002+ | **.NET Framework / .NET** | Managed-ранайм, BCL (`System.*`). WPF, WinForms — тут. |
| 2012+ | **WinRT (Windows Runtime)** | Нові API Windows 8+: камера, Bluetooth, OCR, нотифікації, геолокація. |

**Що «нового» в WinRT:**
- Мовно-незалежний: один `OcrEngine` викликається з C++, C#, JavaScript, Rust через автогенеровані обгортки (**projections**).
- Описаний у `.winmd` (Windows Metadata) — аналог C# IL для метаданих типів.
- Побудований на COM з reference counting, але metadata сучасна.
- Async-first: довгі операції повертають `IAsyncOperation<T>` (аналог Task).

**Чому це «окремий світ» від .NET:**
- Інша система типів: `Windows.Storage.Streams.IRandomAccessStream` ≠ `System.IO.Stream`, `SoftwareBitmap` ≠ `BitmapSource`.
- Інша модель памʼяті: COM-style reference counting (приховано CsWinRT).
- Треба явно включити — через суфікс TFM `net10.0-windows10.0.19041.0`.

**Як з C#:** CsWinRT (C#/WinRT) — генератор, який під час компіляції робить managed-типи-обгортки над WinRT-типами. Пишеш звичайний C#-код, компілятор підставляє потрібний interop.

**Міст між WPF і WinRT.** Їх типи напряму не кастуються. Два способи:
1. Через байтовий потік: encode WPF-bitmap в BMP → MemoryStream → `.AsRandomAccessStream()` → BitmapDecoder → SoftwareBitmap. Простіше коду, трохи overhead.
2. Через піксельний буфер: `BitmapSource.CopyPixels(...)` → `SoftwareBitmap.CreateCopyFromBuffer(...)`. Менше allocations, вимагає знати pixel format і stride.

**Сучасний стек Windows UI:**
- **WinUI 3 / Windows App SDK** — наступне покоління UI, повністю на WinRT.
- **UWP** — попередник, у maintenance mode.
- **Desktop (WPF/WinForms) + TFM-суфікс** — як наш застосунок — може викликати WinRT з «сусіднього світу» для специфічних API (OCR, нотифікації).

## Пакети Windows OCR мов (`Language.OCR~~~*`)
Опціональні компоненти Windows з OCR-моделями для різних мов. Ставляться через:
- PowerShell (адмін): `Add-WindowsCapability -Online -Name 'Language.OCR~~~en-US~0.0.1.0'`
- Settings → Language → Options → Add OCR feature.

Перевірка встановлених: `Get-WindowsCapability -Online | Where-Object Name -like 'Language.OCR*'` (теж з-під адміна).

`Windows.Media.Ocr` використовує саме ці пакети. У коді список доступних мов — `OcrEngine.AvailableRecognizerLanguages`.

## `Microsoft.Extensions.Hosting`
Пакет-«усе в одному», який приносить інфраструктуру як в ASP.NET:
- `Microsoft.Extensions.DependencyInjection` — DI-контейнер.
- `Microsoft.Extensions.Configuration` + JSON + env variables — читання `appsettings.json` і змінних середовища.
- `Microsoft.Extensions.Logging` — `ILogger<T>`.
- `Microsoft.Extensions.Options` — типізований конфіг через `IOptions<T>`.

Для `IHttpClientFactory` окремо знадобиться `Microsoft.Extensions.Http` (додамо на етапі перекладу).

## `AssemblyInfo.cs` — автоген SDK + WPF-стаб
Історично (ще з .NET Framework) кожен проєкт мав `Properties/AssemblyInfo.cs` з assembly-level атрибутами: `AssemblyTitle`, `AssemblyVersion`, `AssemblyFileVersion`, `Guid`, `ComVisible` тощо. Це метадані, зашиті у збірку (видно в Properties → Details exe-шника; читаються через `Assembly.GetExecutingAssembly()`).

У SDK-style проєктах більшість цих атрибутів **автогенерується** під час білду в `obj/Debug/<tfm>/<ProjectName>.AssemblyInfo.cs` на основі властивостей з `.csproj`:
```xml
<PropertyGroup>
  <AssemblyTitle>MyApp</AssemblyTitle>
  <Product>MyApp</Product>
  <Version>0.1.0</Version>
  <Authors>...</Authors>
</PropertyGroup>
```

Відключити автоген: `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>`.

**WPF-окремий стаб.** Шаблон `dotnet new wpf` додатково створює `AssemblyInfo.cs` у корені проєкту з одним атрибутом:
```csharp
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,            // theme-specific resource dict: нема
    ResourceDictionaryLocation.SourceAssembly)] // generic resource dict: у цій же збірці
```

`ThemeInfo` керує resource resolution для WPF: де шукати theme-specific словники (Aero/Dark/Classic — файли типу `Themes/Aero.NormalColor.xaml`) і generic-fallback (`Themes/Generic.xaml`). Критично лише для **бібліотек кастомних контролів** з templated-стилями. Для звичайних застосунків, що юзають стандартні контроли, атрибут нічого не змінює — файл можна сміливо видалити.

## Windows subsystem: `Exe` vs `WinExe`
Windows PE-заголовок позначає exe одним із двох типів:
- `<OutputType>Exe</OutputType>` — console app. Windows при запуску створює чорну консоль, приєднує stdin/stdout/stderr. У CMD/PowerShell переюзає батьківську.
- `<OutputType>WinExe</OutputType>` — GUI app. Windows консолі не створює, вікна малює сам застосунок (WPF, WinForms). stdin/stdout у «нікуди».

`dotnet new wpf` дає `WinExe` — щоб користувач не бачив зайвої консолі поруч із GUI.

## `WinExe` і консольні логи
Наслідки `WinExe` для логування:
- Запуск `dotnet run` з PowerShell: процес стартує, але stdin/stdout не підключені до терміналу. `Console.WriteLine` і `ILogger` → `Console`-провайдер пишуть у «нікуди». Візуально — мигаючий курсор, бо `dotnet run` чекає завершення процесу.
- Де тоді логи: F5 у Visual Studio → вкладка **Output → Debug**. Microsoft.Extensions.Logging.Debug-провайдер пише через `Debug.WriteLine`, дебагер VS це показує.
- Поза дебагером: утиліта DebugView (Sysinternals), слухає `OutputDebugString`.

Коли дійде до релізу — додамо файловий логер (Serilog або built-in file provider), щоб логи зберігалися незалежно від того, як запущено застосунок.

## Патерн `IOptions<T>`
Описуєш POCO-клас з властивостями (`init`-only), прив'язуєш його до секції конфігу через `.AddOptions<T>().Bind(configuration.GetSection("..."))`, і DI-контейнер інжектує екземпляр туди, де потрібно.

## `TaskCompletionSource<T>` — міст подій і async
Звичайний `Task` фреймворк завершує сам (`Task.Run`, `Task.FromResult`). `TaskCompletionSource<T>` (TCS) — це Task, **результатом якого керуєш ти**:
- Створюєш TCS.
- Віддаєш комусь його `.Task` (на нього чекають через `await`).
- Коли вирішиш — викликаєш `tcs.SetResult(value)` → `await` оживає.
- Для винятку — `tcs.SetException(ex)`, `await` викине його.

Використовувати `TrySetResult`/`TrySetException` замість `SetResult`/`SetException` коли результат можуть встановити з кількох шляхів (напр., MouseUp і OnClosed — хто перший, той зафіксував; другий просто повертає `false` без винятку).

**Коли брати TCS замість event-а чи `ShowDialog`-блокування:**
- Код після події треба писати лінійно, не в callback.
- Далі очікуються інші `await` (HTTP, OCR, I/O) — все складеться в один async-ланцюжок.
- Потрібне нормальне error handling через `try/catch` навколо `await`.
- UI не повинен блокуватись nested message loop (`ShowDialog` вмикає такий).

**Коли event / callback вистачить:**
- Результат не потрібен ззовні; обробка живе всередині того, хто піднімає подію.

Головне: TCS — міст між подієвим світом (мишки, Win32-callback-и) і async-світом.

## Синхронізація: `Interlocked`, `SemaphoreSlim`, `lock`, `Mutex`
Коли треба захистити стан від конкурентних читань/записів, у .NET кілька інструментів з різною вагою і призначенням.

| Інструмент | Що це | Коли брати |
|---|---|---|
| `Interlocked.CompareExchange` / `Exchange` / `Increment` | CPU-атомарна інструкція (`lock cmpxchg` на x86), lock-free | Прості прапорці (0/1), лічильники, non-blocking guard. 0 алокацій. |
| `lock(obj) { ... }` | Monitor, блокує поточний потік до звільнення | Короткі sync-only критичні секції. **НЕ** можна з `await` всередині — після await можна опинитись на іншому потоці → `SynchronizationLockException`. |
| `SemaphoreSlim(n, n)` | Managed-об'єкт, рахує слоти (n одночасно) | Async-friendly (`WaitAsync`), скасовуваність через `CancellationToken`, >1 слоту (пул). Алокація + Dispose. |
| `Mutex` | Ядерний об'єкт ОС, може бути named і працювати між процесами | Міжпроцесна синхронізація (single-instance app). Overkill для одного процесу — десятки мікросекунд. |

**Non-blocking guard через Interlocked** (наш випадок в `ScreenTranslationWorkflow`):
```csharp
private int _isBusy;

public async Task DoAsync()
{
    if (Interlocked.CompareExchange(ref _isBusy, 1, 0) != 0)
        return; // уже занят, тихо ігноруємо

    try { /* ... */ }
    finally { Interlocked.Exchange(ref _isBusy, 0); }
}
```

`CompareExchange(ref x, newValue, expected)` — атомарно: *якщо `x == expected`, встав `newValue`; поверни попереднє значення `x`*. Повернулось не `expected` — хтось встиг раніше, значить занят. Reset у `finally` щоб guard не залипав при exception.

**SemaphoreSlim коли повторні виклики мають чекати в черзі:**
```csharp
private readonly SemaphoreSlim _slot = new(1, 1);

public async Task DoAsync()
{
    await _slot.WaitAsync();
    try { /* ... */ }
    finally { _slot.Release(); }
}
```
Відрізняється поведінкою: Interlocked → «ігнорую», SemaphoreSlim → «чекаю черги».

**Правило вибору:**
- Один слот, non-blocking (ігнорую якщо занят) → **`Interlocked`**.
- Один слот, повторні чекають → **`SemaphoreSlim(1, 1)`**.
- Багато слотів (пул) → **`SemaphoreSlim(n, n)`**.
- Простий sync-критичний блок без `await` → **`lock`**.
- Між процесами (single-instance) → **`Mutex`** (named).

Безпека однакова — всі thread-safe при коректному використанні. Різниця в сценаріях і вазі. Для простих guard-ів майже завжди виграє Interlocked.

## Named `Mutex` для single-instance застосунку
`Mutex` без імені — локальний, синхронізує потоки одного процесу (класичний критичний блок). `Mutex` з іменем — **kernel object** Windows, видимий усім процесам, які відкривають його з тим самим ім'ям. У single-instance-сценарії використовується не як критична секція, а як «прапорець у ядрі»: *я є — другого не пускай*.

```csharp
internal sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;

    public bool IsOnlyInstance { get; }

    public SingleInstanceGuard(string name)
    {
        _mutex = new Mutex(initiallyOwned: true, name, out bool createdNew);
        IsOnlyInstance = createdNew;
    }

    public void Dispose() => _mutex.Dispose();
}
```

**Ключові деталі:**
- **`createdNew` out-параметр** — `true`, якщо mutex створено під час цього виклику; `false`, якщо вже існував. Усе, що треба знати: перший екземпляр чи ні.
- **Тримати як field, не local.** GC збере локальну змінну → finalizer звільнить handle → прапорець у ядрі зникне → наступний запуск подумає, що він перший, хоча процес ще живий.
- **`initiallyOwned: true`** — «одразу беру собі». На `createdNew` не впливає, але семантично правильніше для single-instance. З `false` теж працює.
- **Автоматичне звільнення.** Коли процес завершується (хоч через `Exit`, хоч crash), Windows сам звільняє всі kernel-handles — mutex зникає. Lock-файлу такого не дасть — залишиться на диску, наступний запуск думатиме, що хтось живий.

**Ім'я і scope:**
- Без префіксу → **per-session**: кожна Windows-сесія (логін, RDP) має свій простір імен. Інший користувач на тій самій машині запустить свій екземпляр — для tray-застосунку найлогічніше.
- `Global\MyApp-SingleInstance` → **machine-wide**: один екземпляр на всю систему, незалежно від користувача. Треба для сервісів або коли ресурс справді один (COM-порт, device handle).

**Чому не альтернативи:**
- `Process.GetProcessesByName("MyApp")` — race condition між перевіркою і стартом; два екземпляри можуть запуститись майже одночасно і обидва не побачать одне одного. Ім'я процесу ненадійне (перейменований exe).
- Lock-файл на диску — не видаляється при crash, треба писати cleanup з PID-перевіркою.
- Named mutex — атомарна операція «створити або відкрити», race-free, OS сама прибирає.

## `IOptions<T>` — три інтерфейси для різних сценаріїв:
- `IOptions<T>` — snapshot на старті, singleton. Простий кейс, конфіг у рантаймі не міняється. **Наш вибір.**
- `IOptionsSnapshot<T>` — свіжий snapshot на кожен scope. Переважно ASP.NET (scope = HTTP-запит).
- `IOptionsMonitor<T>` — реагує на зміни `appsettings.json` на льоту, має `OnChange(...)`-коллбек.

У коді користуєшся так:
```csharp
public MyService(IOptions<HotkeyOptions> options)
{
    var opts = options.Value; // Value — сам POCO
}
```

Чому не інжектити POCO напряму: `IOptions<T>` додає шар валідації і пост-процесингу, дає можливість пізніше переключитись на monitor без зміни конструктора, і це загальноприйнята конвенція в екосистемі .NET.

## `class` vs `record` vs `struct`
Два різних виміри, які часто плутають:
- **Де лежить:** reference type (heap, передається за посиланням) vs value type (stack/inline, передається копією).
- **Як порівнюється:** reference equality (той самий об'єкт?) vs value equality (однакові поля?).

| Що пишеш | Де лежить | Рівність за замовчуванням |
|---|---|---|
| `class` | heap | reference |
| `record` (= `record class`) | heap | **value** |
| `struct` | stack/inline | value (але `==` писати руками) |
| `record struct` | stack/inline | value (автоматично) |

Тобто `record` ≠ `struct`: за замовчуванням це все ще reference type на heap, просто з value-семантикою рівності.

**Що компілятор дописує в `record`** (на прикладі `public sealed record Foo(int X, string Y);`):
1. Primary constructor `(int x, string y)`.
2. `init`-only властивості `X`, `Y` — immutable після конструктора.
3. `Equals(Foo?)` — поле-за-полем через `EqualityComparer<T>.Default`.
4. `GetHashCode()` — комбінація хешів полів.
5. `==` і `!=` — через `Equals`.
6. `ToString()` — `"Foo { X = 1, Y = ... }"`.
7. `Deconstruct(out ...)` — для `var (x, y) = foo;`.
8. Copy constructor + `with`-expression — `var copy = foo with { X = 42 };`.

Для `class` все це пишеш руками.

**Нюанс value-рівності.** Record порівнює через `EqualityComparer<T>.Default`, тобто **поверхнево**. Reference-поля без переозначеного `Equals` порівняються за посиланням. Для record з полем типу `BitmapSource`: два record з однаковою картинкою, але різними обʼєктами `BitmapSource`, будуть різними.

**Евристика:**
- Entity з identity, стан міняється, має поведінку → `class`.
- DTO / value object, immutable, важить вміст → `record`.
- Дрібна часто копійована структура в hot path → `record struct` або `readonly struct`.
