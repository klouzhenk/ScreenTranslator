# Нотатки: Windows (tray, manifest, OS GUIDs)

## System tray / notification area
Область біля годинника в правому нижньому куті, де сидять іконки фонових застосунків (OneDrive, Steam, Discord). Для tray-only застосунку іконка там + контекстне меню (ПКМ) — головний інтерфейс.

У WPF свого API для трею немає. Два життєздатні варіанти:

**`H.NotifyIcon.Wpf`** — NuGet-пакет. WPF-native, меню в XAML, modern-стиль, без залежності від WinForms. Обрали його (деталі — у `decisions.md`).

**WinForms `NotifyIcon`** — вбудований у .NET, активується через `<UseWindowsForms>true</UseWindowsForms>` у `.csproj`. Класика, перевірено роками, але тягне весь WinForms і меню — зі старим стилем.

**Гачок `TaskbarIcon` з `H.NotifyIcon.Wpf`.** Коли оголошуєш його в `Application.Resources` і тягнеш через `FindResource`, об'єкт створюється, але **не з'являється в треї**. Причина: `TaskbarIcon` — це `FrameworkElement`, і реєстрація в треї через `Shell_NotifyIcon` відбувається при подією `Loaded` (приєднання до visual tree). А в `Application.Resources` visual tree нема → Loaded не фаєриться → хендл не створюється. Фікс: викликати `_trayIcon.ForceCreate()` після `FindResource` — метод примусово ініціалізує хендл.

## Application manifest
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

## `<supportedOS>` і GUID-и Windows
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
