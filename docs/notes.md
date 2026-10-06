# Нотатки по ходу роботи

Пояснення концепцій, які виникали під час розробки ScreenTranslator. Короткі довідки — не підручник, а шпаргалка повернутися й пригадати. Рішення (чому обрали X, а не Y) — у `decisions.md`.

## Git

### `git branch -M <name>`
Перейменовує поточну гілку. Велика `-M` = force rename (не впаде, якщо гілка з такою назвою вже існує). Корисно після `git init` на старих версіях Git, де дефолт `master`: `git branch -M main` гарантує, що опинишся на `main` незалежно від конфігу. На нових Git дефолт і так `main` — команда ідемпотентна.

## .NET

### TargetFramework `net10.0-windows10.0.19041.0`
Складений TFM з трьох частин:
- `net10.0` — рантайм .NET 10.
- `-windows` — Windows-специфічне API: WPF, WinForms, Win32 interop.
- `10.0.19041.0` — мінімальна версія Windows SDK, яка відкриває **WinRT** (Windows Runtime) з .NET.

Без суфікса `10.0.19041.0` неймспейси `Windows.*` (наприклад `Windows.Media.Ocr`) недоступні — компілятор їх не бачить, бо не генерується C# interop для WinRT (via [CsWinRT](https://github.com/microsoft/CsWinRT)).

Число `19041` відповідає Windows 10 версія 2004 (травень 2020) — стабільний мінімум для сучасного WinRT. Бувають вищі (`22621`, `26100`), але для `Windows.Media.Ocr` достатньо.

Побічний ефект: тулінг виставить `<SupportedOSPlatformVersion>` = 10.0.19041.0, компілятор буде лаятись при зверненні до API з новіших Windows.

### Пакети Windows OCR мов (`Language.OCR~~~*`)
Опціональні компоненти Windows з OCR-моделями для різних мов. Ставляться через:
- PowerShell (адмін): `Add-WindowsCapability -Online -Name 'Language.OCR~~~en-US~0.0.1.0'`
- Settings → Language → Options → Add OCR feature.

Перевірка встановлених: `Get-WindowsCapability -Online | Where-Object Name -like 'Language.OCR*'` (теж з-під адміна).

`Windows.Media.Ocr` використовує саме ці пакети. У коді список доступних мов — `OcrEngine.AvailableRecognizerLanguages`.

### `Microsoft.Extensions.Hosting`
Пакет-«усе в одному», який приносить інфраструктуру як в ASP.NET:
- `Microsoft.Extensions.DependencyInjection` — DI-контейнер.
- `Microsoft.Extensions.Configuration` + JSON + env variables — читання `appsettings.json` і змінних середовища.
- `Microsoft.Extensions.Logging` — `ILogger<T>`.
- `Microsoft.Extensions.Options` — типізований конфіг через `IOptions<T>`.

Для `IHttpClientFactory` окремо знадобиться `Microsoft.Extensions.Http` (додамо на етапі перекладу).

### `AssemblyInfo.cs` — автоген SDK + WPF-стаб
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

### Windows subsystem: `Exe` vs `WinExe`
Windows PE-заголовок позначає exe одним із двох типів:
- `<OutputType>Exe</OutputType>` — console app. Windows при запуску створює чорну консоль, приєднує stdin/stdout/stderr. У CMD/PowerShell переюзає батьківську.
- `<OutputType>WinExe</OutputType>` — GUI app. Windows консолі не створює, вікна малює сам застосунок (WPF, WinForms). stdin/stdout у «нікуди».

`dotnet new wpf` дає `WinExe` — щоб користувач не бачив зайвої консолі поруч із GUI.

### `WinExe` і консольні логи
Наслідки `WinExe` для логування:
- Запуск `dotnet run` з PowerShell: процес стартує, але stdin/stdout не підключені до терміналу. `Console.WriteLine` і `ILogger` → `Console`-провайдер пишуть у «нікуди». Візуально — мигаючий курсор, бо `dotnet run` чекає завершення процесу.
- Де тоді логи: F5 у Visual Studio → вкладка **Output → Debug**. Microsoft.Extensions.Logging.Debug-провайдер пише через `Debug.WriteLine`, дебагер VS це показує.
- Поза дебагером: утиліта DebugView (Sysinternals), слухає `OutputDebugString`.

Коли дійде до релізу — додамо файловий логер (Serilog або built-in file provider), щоб логи зберігалися незалежно від того, як запущено застосунок.

### Патерн `IOptions<T>`
Описуєш POCO-клас з властивостями (`init`-only), прив'язуєш його до секції конфігу через `.AddOptions<T>().Bind(configuration.GetSection("..."))`, і DI-контейнер інжектує екземпляр туди, де потрібно.

Три інтерфейси для різних сценаріїв:
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

### `class` vs `record` vs `struct`
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

## WPF / XAML

### Що таке XAML
XML-діалект для опису UI і налаштувань WPF-застосунку. Кожен `.xaml` має «брата» `.xaml.cs` (code-behind) — разом вони одна `partial class`: XAML описує структуру, C# — поведінку.

### Структура `App.xaml`
```xml
<Application x:Class="ScreenTranslator.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <!-- глобальні ресурси: стилі, кисті, конвертери -->
    </Application.Resources>
</Application>
```

- `<Application>` — клас, що представляє застосунок, успадкований від `System.Windows.Application`.
- `x:Class` — повне ім'я C#-класу, з яким цей XAML парується.
- `xmlns` — основний простір імен WPF (`<Window>`, `<Button>` і т.д.).
- `xmlns:x` — службовий XAML (`x:Class`, `x:Name`, `x:Key`) — як `using` для XAML.
- `Application.Resources` — глобальний ResourceDictionary.

Атрибут `StartupUri="MainWindow.xaml"` вказує WPF автоматично створити і показати це вікно при старті. Для tray-застосунку його прибирають, а життєвий цикл керується з `App.xaml.cs` (override `OnStartup`/`OnExit`).

### XAML namespaces і `x:`-префікс
Два `xmlns` на корені будь-якого WPF-файла:
```xml
xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
```

URL-и — просто **ідентифікатори**, нічого не завантажується з інтернету. У збірках зареєстровано, який namespace відповідає якому URL.

- **Без префікса** — простір WPF-контролів (`Window`, `Grid`, `Button`, `Image`, ...).
- **`xmlns:x`** — простір самої мови XAML, інструкції для компілятора.

Що дає `x:`:

| Атрибут | Що означає |
|---|---|
| `x:Class="Namespace.ClassName"` | На корені: прошʼй цей XAML з таким C#-класом. Міст між XAML і code-behind. |
| `x:Name="SomeId"` | Дай елементу імʼя, яке стане полем у згенерованому code-behind. Після цього з C# — `SomeId.Property = ...`. |
| `x:Key="SomeKey"` | Ключ у `ResourceDictionary` (пригадай `<tb:TaskbarIcon x:Key="TrayIcon">`). |
| `x:Static="Ns.Class.Field"` | Доступ до статичного члена C# з XAML. |

Правило: **`x:` — метаінструкція для XAML-компілятора**, не властивість елемента.

### XAML attributes = C# properties
Будь-який атрибут без префікса — це присвоєння `public`-властивості. `Title="Preview"` → `window.Title = "Preview"`. `Background="Black"` → через TypeConverter рядок конвертується в `SolidColorBrush`. Властивості наслідуються по ланцюжку `Window` → `ContentControl` → `Control` → `FrameworkElement` → `UIElement` → `Visual` → `DependencyObject` — все звідти доступне.

### Content model
`ContentControl` (напр., `Window`, `Button`) має властивість `Content` типу `object`. Все, що між `<Window>` і `</Window>` в XAML, автоматично йде в `Content`. Але це **один** елемент — тому як обгортку ставлять `Panel`-контейнер (`Grid`, `StackPanel`, `DockPanel`), який тримає **колекцію** дітей через `Children`.

### Dependency properties — чому «state changed» не потрібен
У WPF властивості на UI-елементах (`Width`, `Height`, `Canvas.Left`, `Fill`, `Visibility`, `Opacity`...) — не звичайні C#-properties, а **dependency properties** (DP). Вони зареєстровані через `DependencyProperty.Register` і мають систему оповіщень вшиту в інфраструктуру.

Коли робиш `SelectionRect.Width = 100` або `Canvas.SetLeft(rect, 50)`, під капотом:
1. DP-система зберігає нове значення, порівнює зі старим.
2. Викликає зареєстрований `OnPropertyChanged`-callback.
3. Для layout-affecting властивостей — `InvalidateMeasure()` + `InvalidateArrange()` → перерахунок layout на наступному tick.
4. Для render-affecting — `InvalidateVisual()` → перемалювання.

Все автоматично. Ти лише присвоюєш — WPF сам оновлює UI.

**Коли треба `INotifyPropertyChanged` руками:**

| Випадок | Треба? |
|---|---|
| Присвоюєш властивість на WPF-елементі (`Width`, `Fill`, `Canvas.Left`) | Ні — DP всередині. |
| ViewModel-поле, що біндиться в XAML (`{Binding UserName}`) | Так — інакше binding не впіймає зміни. |
| Поле в code-behind, яке сам читаєш/присвоюєш WPF-елементам | Ні — зачіпаєш DP прямо. |

ViewModel зазвичай не наслідує `DependencyObject`, тому плюшок DP у неї нема. Треба `INotifyPropertyChanged` руками (або `CommunityToolkit.Mvvm` з source generator).

Різниця з WinForms/React: там типово `state.x = ...; redraw()`. У WPF `redraw()` не викликають — властивості «знають, що робити».

### Attached properties: `Canvas.SetLeft`, `Grid.SetRow`
Attached property — DP, яка **належить одному класу**, а виставляється **на іншому**. Приклад: `Canvas.Left` належить `Canvas`, але виставляється на елементах, покладених у Canvas:

```xml
<Canvas>
    <Rectangle Canvas.Left="100" Canvas.Top="50" Width="20" Height="20"/>
</Canvas>
```

У коді — через статичні методи `Canvas.SetLeft(element, value)` / `Canvas.GetLeft(element)`. Patterns такі:
- `Canvas.Left/Top` — абсолютне позиціонування в Canvas.
- `Grid.Row/Column/RowSpan/ColumnSpan` — розташування в сітці.
- `DockPanel.Dock` — до якої сторони притиснути в DockPanel.
- `Panel.ZIndex` — порядок по Z.

Прочитується так: «елемент не знає, що він у Canvas-і, але Canvas на нього дивиться і запитує: `GetLeft(child)` — і розташовує за цим значенням». Контейнер керує позиціонуванням, діти лише зберігають hint для нього.

## Interop

### P/Invoke
Platform Invoke — механізм .NET для виклику функцій з нативних DLL (user32.dll, kernel32.dll, довільні C-бібліотеки). Описуєш C#-«відповідник» зовнішньої функції з атрибутом, а runtime або source generator бере на себе маршалінг аргументів.

### Win32 скорочення
API Win32 — 30+ років, писано для C з короткими іменами. Головні префікси:

| Префікс | Розшифровка | Приклад |
|---|---|---|
| `WM_` | Window Message | `WM_HOTKEY`, `WM_CLOSE`, `WM_PAINT` |
| `HWND` | Handle to Window | хендл вікна (не вказівник, а токен) |
| `MOD_` | Modifier | `MOD_CONTROL`, `MOD_ALT` |
| `VK_` | Virtual Key | `VK_T`, `VK_F1`, `VK_RETURN` — код клавіші, не залежить від розкладки |
| `h<щось>` | handle | `hWnd` (вікно), `hDC` (device context), `hBitmap` |
| `fs<щось>` | flag set | `fsModifiers` — бітмаска модифікаторів |

**Hungarian notation.** Імена змінних типу `hWnd`, `lParam`, `wParam`, `cbSize` — перші літери кодують тип: `h` handle, `l` long, `w` word (16-bit), `cb` count of bytes, `fs` flag set. Сьогодні в C# так не пишуть, але Win32-ний API — його мова.

### Marshalling
Конвертація даних між .NET-ним і нативним представленням при перетині managed/unmanaged бар'єра. Потрібна, бо пам'ять і типи різні:

| Що | .NET | Native (C/Win32) |
|---|---|---|
| Рядок | `System.String`, UTF-16, immutable, managed heap | `char*` (ANSI) / `wchar_t*` (UTF-16), null-term |
| `bool` | 1 байт | `BOOL` = 4 байти (`int`) |
| Пам'ять | GC може переміщати | Фіксовані адреси |
| Структури | Можуть містити reference | Flat memory layout |

Marshaller — компонент, що виділяє буфери, копіює/конвертує, pin'ить managed-пам'ять (щоб GC її не рухав під час виклику), і робить зворотнє при поверненні.

### `DllImport` vs `LibraryImport`
Два способи оголосити P/Invoke:
- **`[DllImport]`** (класика) — marshalling-код генерується в рантаймі через reflection + IL emit. Повільніший старт, не AOT-friendly, «чорна скринька».
- **`[LibraryImport]`** (сучасний, .NET 7+) — marshalling генерується **під час компіляції** через source generator. Швидше, AOT-friendly, код можна подивитись (під `obj/`).

Вимагає від класу бути `partial`, а методів без тіла — `partial` з модифікатором. У `.csproj` треба `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` — generator пише код із сирими вказівниками. Це **не** дозвіл писати unsafe вручну (хоча можна); твого коду вимога не торкається.

Для нових проєктів однозначно `LibraryImport`.

### `LibraryImports.g.cs` — звідки береться
Коли оголошуєш `partial`-метод з `[LibraryImport]` без тіла — хто його реалізує?

**Roslyn source generators** — плагіни до компілятора, які під час компіляції генерують додаткові `.cs`-файли і додають їх у проєкт. Для `LibraryImport` це `LibraryImportGenerator`. Він знаходить усі `partial`-методи з цим атрибутом і генерує **другу частину partial-класу** з реальним тілом (fixed-блоки, GCHandle, маршалінг, перевірка last-error) у файл:

```
obj/Debug/<tfm>/generated/Microsoft.Interop.LibraryImportGenerator/.../LibraryImports.g.cs
```

Суфікс **`.g.cs`** = generated, IDE показує read-only. По `F12` з виклику ведеш саме туди.

**Чому іноді «спочатку еррори, потім зникли».** IntelliSense (швидкий аналізатор) бачить `partial`-метод без тіла і одразу малює помилку. Source generator — повільніший окремий етап. Поки він працює, IntelliSense ще не знає про згенерований код. Коли generator закінчує — файл з'являється, помилки зникають. Класична гонка в VS/Rider; інколи треба зберегти ще раз або зробити Build, щоб синхронізувалося.

### GDI: screen DC vs memory DC
DC (Device Context) — не bitmap, а **контекст** малювання: набір налаштувань + поточно вибраний bitmap/brush/pen/font. Два види, які зустрічаються при скриншотингу:

- **screenDC** — DC екрану (framebuffer-а). Отримуємо через `GetDC(desktop)`, звільняємо через `ReleaseDC`. Повʼязаний з реальним кольоровим framebuffer-ом ОС, його bit depth = bit depth екрану (зазвичай 32 bpp).
- **memoryDC** — DC у памʼяті процесу, віртуальне полотно. Створюємо через `CreateCompatibleDC`, видаляємо через `DeleteDC`. Свіжий memoryDC тримає **1×1 монохромний stub-bitmap** як «тимчасову поверхню», поки ми не вибрали туди свій bitmap через `SelectObject`.

Аналогія: screenDC — реальна картина на стіні (можна лише дивитись); memoryDC — чистий мольберт на столі, на який клеїмо аркуш (bitmap) і копіюємо туди картину.

**Критичне правило `CreateCompatibleBitmap`.** Функція створює bitmap з **bit depth того DC**, який їй передали:
- `CreateCompatibleBitmap(screenDC, w, h)` → колірний bitmap (32 bpp). ✅
- `CreateCompatibleBitmap(memoryDC, w, h)` **до** `SelectObject` → 1 bpp монохром (бо memoryDC тримає 1bpp stub). ❌
- `CreateCompatibleBitmap(memoryDC, w, h)` **після** вибору color-bitmap у memoryDC → колір (унаслідує від поточного).

Правило запамʼятати: для `CreateCompatibleBitmap` **бери screenDC** — DC, повʼязаний з реальною кольоровою поверхнею. Memory DC — для вибору bitmap-а, не для шаблону. Якщо проігнорувати — скриншот виходить чорно-білий, `BitmapSource.Format` показує `Indexed1`.

Цикл правильно:
1. `screenDC = GetDC(desktop)`
2. `memoryDC = CreateCompatibleDC(screenDC)` — memoryDC з 1bpp stub, але знає про 32bpp screenDC.
3. `bitmap = CreateCompatibleBitmap(screenDC, w, h)` — color bitmap.
4. `SelectObject(memoryDC, bitmap)` — підмінили stub на наш color-bitmap.
5. `BitBlt(memoryDC, …, screenDC, …)` — тепер копіюємо кольорово.

### ROP codes (Raster Operations)
Код, який каже `BitBlt` та іншим GDI-функціям **як комбінувати** піксели source і destination при копіюванні. Спадок з Windows 1.0 (1985), коли GDI вмів побітові операції прямо під час копіювання.

У BitBlt три «джерела»: **S** (source), **D** (destination), **P** (pattern/brush). ROP описує булеву формулу над ними.

| Константа | Hex | Формула | Що робить |
|---|---|---|---|
| `SRCCOPY` | `0x00CC0020` | `D = S` | Просто скопіювати. **Наш випадок.** |
| `SRCINVERT` | `0x00660046` | `D = S XOR D` | XOR — класичне «малювання курсора», бо два XOR повертають оригінал. |
| `SRCAND` | `0x008800C6` | `D = S AND D` | Маска. |
| `SRCPAINT` | `0x00EE0086` | `D = S OR D` | Накласти source зверху. |
| `WHITENESS` | `0x00FF0062` | `D = 1` | Залити білим. |
| `BLACKNESS` | `0x00000042` | `D = 0` | Залити чорним. |
| `PATCOPY` | `0x00F00021` | `D = P` | Залити pattern. |

Всього 256 варіантів (2^(2^3)). У верхньому байті hex закодована таблиця істинності формули (напр., `CC` = `11001100` = `D = S`), у молодших — метадані для GDI.

## Windows

### System tray / notification area
Область біля годинника в правому нижньому куті, де сидять іконки фонових застосунків (OneDrive, Steam, Discord). Для tray-only застосунку іконка там + контекстне меню (ПКМ) — головний інтерфейс.

У WPF свого API для трею немає. Два життєздатні варіанти:

**`H.NotifyIcon.Wpf`** — NuGet-пакет. WPF-native, меню в XAML, modern-стиль, без залежності від WinForms. Обрали його (деталі — у `decisions.md`).

**WinForms `NotifyIcon`** — вбудований у .NET, активується через `<UseWindowsForms>true</UseWindowsForms>` у `.csproj`. Класика, перевірено роками, але тягне весь WinForms і меню — зі старим стилем.

**Гачок `TaskbarIcon` з `H.NotifyIcon.Wpf`.** Коли оголошуєш його в `Application.Resources` і тягнеш через `FindResource`, об'єкт створюється, але **не з'являється в треї**. Причина: `TaskbarIcon` — це `FrameworkElement`, і реєстрація в треї через `Shell_NotifyIcon` відбувається при подією `Loaded` (приєднання до visual tree). А в `Application.Resources` visual tree нема → Loaded не фаєриться → хендл не створюється. Фікс: викликати `_trayIcon.ForceCreate()` після `FindResource` — метод примусово ініціалізує хендл.

### Application manifest
XML-метадані для Windows-завантажувача: «як поводитися з цим `.exe` **до** того, як виконається перший рядок коду».

**Де живе.** Два варіанти:
- Окремий файл `MyApp.exe.manifest` поряд з `.exe` (Windows шукає автоматично).
- **Вбудований як ресурс** у сам `.exe` (`RT_MANIFEST`, resource id = 1). MSBuild робить саме так, коли вказуєш `<ApplicationManifest>app.manifest</ApplicationManifest>` у `.csproj`. Другий варіант кращий — один файл, нічого не загубиш.

**Коли читають.** До `Main()`. Windows-loader парсить його, поки мапить `.exe` у пам'ять процесу. Саме тому DPI awareness задають у маніфесті — його треба виставити до створення першого вікна, інакше Windows уже «вирішить» за нас.

**Що в ньому буває.**

| Секція | Для чого |
|---|---|
| `<dpiAwareness>` | Рівень DPI awareness (наш випадок — `PerMonitorV2`). |
| `<requestedExecutionLevel>` | UAC-рівень: `asInvoker`, `requireAdministrator`, `highestAvailable`. |
| `<supportedOS>` | GUID-и Windows-версій, на яких тестувалися (див. нижче). |
| `<longPathAware>` | «Я вмію шляхи > 260 символів». |
| `<activeCodePage>` | UTF-8 як default для ANSI-API (Windows 10 1903+). |
| `<assemblyIdentity>` + `<dependency>` | Side-by-side assemblies: прив'язка до конкретних версій comctl32/COM-компонентів. |

**Аналогія.** `.csproj` → для build-системи («мені треба такий SDK, такі пакети»). Маніфест → для Windows loader («мені треба така поведінка ОС, я тестована там-то, я вмію ось це»).

**Чому не з коду.** Для DPI awareness є `SetProcessDpiAwarenessContext(...)`, але викликати його треба **раніше** за будь-який `HWND`. У WPF це крихкий момент — внутрішні виклики легко створюють вікно раніше, ніж до нього дійдеш. Маніфест вирішує раз і назавжди: завантажувач читає його **до** старту CLR.

### `<supportedOS>` і GUID-и Windows
Microsoft закодувала кожну «велику» Windows унікальним GUID. Якщо застосунок **не** декларує GUID, Windows вважає його старим і включає compat-shims (наприклад, `GetVersionEx` поверне «Windows 8» навіть на Windows 11, щоб старі перевірки версій не ламались).

| Windows | GUID |
|---|---|
| Vista | `{e2011457-1546-43c5-a5fe-008deee3d3f0}` |
| 7 | `{35138b9a-5d96-4fbd-8e2d-a2440225f93a}` |
| 8 | `{4a2f28e3-53b9-4441-ba9c-d69d4a4a6e38}` |
| 8.1 | `{1f676c76-80e1-4239-95bb-83d0f6d0da78}` |
| 10 і 11 | `{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}` |

Windows 11 **не** має окремого GUID — ділить з 10, бо з точки зору сумісності застосунків — один набір API.

Можна вказувати кілька `<supportedOS>` одразу (якщо застосунок знає і 8.1, і 10/11) — Windows вибере найпідхожий. GUID замість рядка типу `"Windows 10"` — щоб: (а) опт-ін був усвідомленим (треба явно скопіювати з Microsoft Learn), (б) не залежати від перекладів назв Windows, (в) старі парсери просто ігнорували unknown-елемент замість того, щоб ламатись.
