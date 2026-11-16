# Версійність застосунку

## Три основні схеми

### 1. SemVer (Semantic Versioning) — `MAJOR.MINOR.PATCH`
Найпоширеніша в open-source і бібліотеках. Правила:
- **PATCH** (0.1.**1** → 0.1.**2**) — bug fix, зворотньо сумісно.
- **MINOR** (0.**1**.0 → 0.**2**.0) — нова фіча, зворотньо сумісно.
- **MAJOR** (**0**.1.0 → **1**.0.0) — breaking change, користувач повинен щось переробити.

Приклад читання: `H.NotifyIcon.Wpf 2.4.1` — 2-га мажорна (був breaking change з 1.x), 4-та мінорна (додавалися фічі), 1-й патч (один фікс після 2.4.0). Три числа одразу розказують історію.

**Нюанс `0.x`.** За домовленістю `0.x.y` означає «API ще нестабільний, може зламатися». Тому бібліотеки в `0.x` можуть різати фічі в MINOR без PR-скандалу. Перехід на `1.0.0` — публічна декларація «тепер стабільно, breaking changes вагомі».

### 2. CalVer (Calendar Versioning) — `YYYY.MM.PATCH`
Версія = дата релізу. Ubuntu `24.04`, PyCharm `2026.1`, Visual Studio Code (формально SemVer, фактично CalVer по релізному циклу).

Коли брати: продукт розвивається неперервно, без чітких «а, це ж MAJOR». Користувачу важливіше «наскільки це свіже», а не «чи зламається моя інтеграція».

### 3. «Marketing» / milestone версії — `0.1`, `0.2`, `1.0`
Без формальних правил, просто цифри, що передають стадію. `0.1` — перший drop, `0.5` — половина шляху, `1.0` — «це готово», `2.0` — «переписав з нуля». Поширено в pet-проєктах і indie-іграх.

## Коли версійність має сенс

**Версія важлива, коли:**
- Користувачі кажуть «у мене версія X, фіча Y не працює» — треба знати, чи це баг конкретного релізу.
- Хтось залежить від твого API — явно сигналити breaking changes.
- Installer / OS вирішують «це оновлення чи переустановка?» за версією.
- Кілька версій існують паралельно (stable, beta, nightly).

**Версія — ритуал, коли:**
- Юзер — ти сам(а), все пам'ятаєш з git log.
- Залежностей від твого коду немає.
- Релізи ad-hoc, «коли дошліфую».

Для *більшості* pet-проєктів — 2-й сценарій. Для десктоп-застосунку з installer'ом — таки 1-й, бо Windows хоче знати версію для показу в «Програми й компоненти» і для детекції upgrade vs re-install.

## Три джерела версії в .NET + Inno Setup

Усі три повинні бути **в unison**:

### `.csproj` — через MSBuild
```xml
<PropertyGroup>
  <Version>0.1.0</Version>
</PropertyGroup>
```

Один цей рядок встановлює:
- `AssemblyInformationalVersion` → `"0.1.0"` (semver рядок, читається через `FileVersionInfo.ProductVersion`).
- `AssemblyVersion` → `0.1.0.0` (числове 4-part для .NET type resolution).
- `FileVersion` → `0.1.0.0` (Windows показує в Properties → Details → File version).

Після `dotnet publish` клацнеш правою на exe → Properties → Details — побачиш версію там.

**Деталізувати можна окремо**, якщо треба різні числа:
```xml
<AssemblyVersion>0.1.0.0</AssemblyVersion>
<FileVersion>0.1.0.0</FileVersion>
<InformationalVersion>0.1.0-beta+build.42</InformationalVersion>
```
Але для MVP достатньо одного `<Version>`.

### `.iss` — через preprocessor `#define`
```iss
#define MyAppVersion "0.1.0"
...
[Setup]
AppVersion={#MyAppVersion}
```
Windows при встановленні читає `AppVersion` і зберігає в Uninstall-registry. Саме це значення Windows використовує для:
- Відображення в «Програми й компоненти».
- Детекції upgrade vs нова установка (при тому самому `AppId`).

### Git tag
```powershell
git tag -a v0.1.0 -m "First MVP release"
git push --tags
```
Не впливає на бінарник. Але позначає коміт, з якого зроблено реліз — завжди можна повернутись у «який код був у v0.1.0?».

**Annotated tag (`-a`) vs lightweight:**
- **Annotated** (`git tag -a v0.1.0 -m "..."`) — повноцінний git object з автором, датою, повідомленням. GitHub показує на Releases page з метаданими.
- **Lightweight** (`git tag v0.1.0`) — просто вказівник на коміт. Для релізу беруть annotated.

## Як Windows детектує upgrade

Коли користувач запускає твій новий installer, Windows дивиться на **`AppId`** в Registry:
- `AppId` **збігається** з уже встановленим → це той самий продукт. Порівнює `AppVersion`:
  - **Новіша** → upgrade: прибирає стару, ставить нову, зберігає shortcut'и.
  - **Та сама або нижча** → Inno Setup покаже «Ця версія вже встановлена, перевстановити?».
- `AppId` **різний** → окремий продукт. Встановлюється поряд.

Тому:
- **`AppId` — продукт**, фіксований на весь час життя застосунку. Міняти — тільки якщо реально робиш окремий новий продукт.
- **`AppVersion` — реліз**, росте з кожним випуском.

Якщо версію не збільшити, а зібрати новий installer — Windows буде думати, що це та сама версія, і може відмовитися ставити.

## Релізний ритуал

Для ScreenTranslator — коли новий реліз:
1. Оновити `<Version>0.2.0</Version>` в `.csproj`.
2. Оновити `#define MyAppVersion "0.2.0"` в `.iss`.
3. `dotnet publish -c Release`.
4. F9 в Inno Setup → свіжий `Setup.exe`.
5. `git commit -am "bump version to 0.2.0"`.
6. `git tag -a v0.2.0 -m "..."` → `git push --tags`.
7. Опційно GitHub Release з прикріпленим `Setup.exe` як asset.

## Автоматизація

Якщо ритуал «оновити в двох місцях + tag + release» звучить як overhead:

**Автогенерація з git tag.** В `.csproj`:
```xml
<Version Condition="'$(Version)' == ''">0.0.0-dev</Version>
```
В CI-пайплайні встановлювати:
```powershell
dotnet publish -c Release -p:Version=$(git describe --tags --abbrev=0)
```
Тоді `git tag v0.2.0` — і `dotnet publish` автоматично бере версію з тега. Але це вже CI-територія, для hand-made release поки не треба.

**MSBuild може підхоплювати `.csproj` у `.iss`** через PowerShell preprocessor-виклик, але це перебір для MVP.

## Для ScreenTranslator — рекомендація

**SemVer, починаючи з `0.1.0`.** Причини:
- MVP готовий, але в активному розвитку. `0.x` чесно сигналить «ще може мінятися».
- Простий і звичний.
- Коли буде фіч достатньо — тегнеш `1.0.0`.

Адаптовані для pet-проєкту правила:
- **PATCH** — виправила OCR на стилізованих шрифтах, прибрала падіння → 0.1.0 → 0.1.1.
- **MINOR** — додала UI налаштувань, словничок, редагування OCR тексту → 0.1.1 → 0.2.0.
- **MAJOR** — переписала на WinUI 3 або поламала конфіг-формат → 0.9.x → 1.0.0.

## Див. також
- [Публікація .NET-застосунку](release-publishing.md) — як `<Version>` взаємодіє з `publish`.
- [Inno Setup](inno-setup.md) — де задається `AppVersion` і `AppId`.
