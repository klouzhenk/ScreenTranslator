# Inno Setup — installer для Windows

## Що це
Безкоштовний installer-builder для Windows від Jordan Russell, створений 1997 року, досі активно розвивається. Пакує твою папку з файлами в `Setup.exe`, який користувач клацає і отримує звичний «майстер встановлення» з UAC, shortcut'ами, деінсталятором і прописом у «Програми і компоненти».

## Workflow
1. Пишеш `.iss` текстовий скрипт (нижче).
2. Відкриваєш у Inno Setup Compiler → **Build → Compile** (F9).
3. На виході `Setup.exe` з усіма твоїми файлами всередині (архів зі stub-розпаковувачем).
4. Користувач запускає `Setup.exe` → проходить майстер → твої файли опиняються в `{app}`, shortcut'и створені, uninstaller зареєстрований.

## Синтаксис — гібрид з кількох джерел
| Елемент | Звідки | Приклад |
|---|---|---|
| `[Section]` + `key=value` | INI | `AppName=ScreenTranslator` |
| `Name: ...; Flags: ...` | Власне розширення | `Name: "autostart"; Flags: unchecked` |
| `#define`, `{#Macro}` | C preprocessor | `#define Ver "0.1.0"` → `{#Ver}` |
| `[Code]` з `begin`/`end` | Delphi/Pascal | Для custom-логіки, частіше необовʼязково |
| `{app}`, `{localappdata}` | Inno constants | Плейсхолдери-змінні |

## Основні секції
| Секція | Що містить |
|---|---|
| `[Setup]` | Метадані застосунку, куди встановлювати, компресія, UAC-політика |
| `[Languages]` | Мови UI інсталятора (`.isl`-файли з Languages/ при Inno) |
| `[Tasks]` | Опційні чекбокси для користувача (desktop icon, autostart) |
| `[Files]` | Що копіювати з якої вихідної папки |
| `[Icons]` | Shortcut'и в меню Пуск і на робочому столі |
| `[Registry]` | Запис у реєстр (autostart, file associations) |
| `[Run]` | Що виконати після встановлення |
| `[UninstallRun]` | Що виконати при деінсталяції |
| `[Code]` | Pascal-функції для складної логіки (перевірка .NET, custom pages) |

## Ключові `[Setup]`-властивості
- `AppId={{GUID}` — унікальний GUID, щоб Windows відрізняв оновлення від окремого застосунку. **`{{` обовʼязково** — escape для літеральної `{`.
- `DefaultDirName={localappdata}\Programs\{#MyAppName}` — куди ставити за замовчуванням.
- `PrivilegesRequired=lowest` — без UAC, per-user install.
- `PrivilegesRequired=admin` — з UAC, Program Files, machine-wide install.
- `Compression=lzma2`, `SolidCompression=yes` — макс компресія.
- `WizardStyle=modern` — сучасний UI (без нього — 1998-style).
- `UninstallDisplayIcon={app}\MyApp.exe` — іконка в «Програми і компоненти».
- `DisableProgramGroupPage=yes` — пропустити сторінку «виберіть папку в меню Пуск», використати дефолт.

## Корисні constants
| Constant | Розгортається в |
|---|---|
| `{app}` | Папка встановлення (значення `DefaultDirName`) |
| `{localappdata}` | `C:\Users\<user>\AppData\Local` |
| `{userappdata}` | `C:\Users\<user>\AppData\Roaming` |
| `{commonappdata}` | `C:\ProgramData` |
| `{pf}` / `{pf32}` / `{pf64}` | `C:\Program Files` / `Program Files (x86)` / `Program Files` (x64) |
| `{group}` | Папка в меню Пуск для shortcut'ів |
| `{autodesktop}` | Робочий стіл (per-user або all-users залежно від UAC) |
| `{tmp}` | Тимчасова папка інсталятора |

## Корисні `Flags` (для `[Files]`, `[Icons]`, `[Registry]`)
| Flag | Значення |
|---|---|
| `ignoreversion` | Копіювати файл без перевірки версії (завжди перезаписувати) |
| `recursesubdirs` | Для `Source: "folder\*"` — також копіювати підпапки |
| `createallsubdirs` | Створити порожні підпапки, навіть якщо в них нема файлів |
| `onlyifdoesntexist` | Не перезаписувати існуючий (корисно для config'ів) |
| `uninsneveruninstall` | НЕ видаляти при деінсталяції (для user data) |
| `uninsdeletevalue` | Для `[Registry]` — видалити ключ при деінсталяції |
| `postinstall` | Чекбокс «Запустити зараз» наприкінці майстра |
| `nowait` | Для `[Run]` — не чекати завершення процесу |
| `unchecked` | Для `[Tasks]` — чекбокс спочатку виключений |

## Autostart через `[Registry]`
```iss
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "{#MyAppName}"; \
  ValueData: """{app}\{#MyAppExeName}"""; \
  Flags: uninsdeletevalue; Tasks: autostart
```
Windows при логіні користувача запускає все, що перераховане в цьому ключі. Потрійні лапки навколо шляху — екранування всередині рядка (`""` → одна `"`), щоб шлях з пробілами коректно опинився в реєстрі.

## Збереження user config при оновленні
Проблема: при оновленні застосунку інсталятор перезапише `appsettings.json`, стерши налаштування користувача.

Рішення на рівні installer:
```iss
Source: "appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall
```
- `onlyifdoesntexist` — не перезаписувати існуючий файл при оновленні.
- `uninsneveruninstall` — залишити файл при деінсталяції (якщо хочеш, щоб перевстановлення піднімало попередні налаштування).

Краще рішення на рівні коду — defaults + user overrides паттерн (див. `release-publishing.md`), тоді interact не потрібен.

## Альтернативи
| Інструмент | Коли брати |
|---|---|
| **Inno Setup** | Простий десктоп-застосунок, user-friendly майстер |
| **WiX Toolset** | Складна enterprise-установка, group policy, GPO deployment — Microsoft's MSI |
| **MSIX** | Сучасний Microsoft-формат, автооновлення з Microsoft Store, sandbox. Жорсткіші обмеження щодо реєстру/файлів |
| **NSIS** | Легший за Inno, трохи «ретро», Script-based (власна мова) |
| **ClickOnce** | Застарілий, тільки для простих .NET-застосунків без адмін-прав |

Для hobby-проєктів, pet-застосунків, demo для команди — Inno Setup попадає в точку «мінімум зусиль, максимум нормального UX».

## Де довідка
- F1 в Inno Setup Compiler → локальний chm-help з усіма секціями, ключами, constants, flags.
- https://jrsoftware.org/ishelp/ — та сама довідка онлайн.

## Для ScreenTranslator
Див. `installer/ScreenTranslator.iss` — конкретний скрипт для нашого випадку.
