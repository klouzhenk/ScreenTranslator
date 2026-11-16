# Публікація .NET-застосунку

## `build` vs `publish` — що різного
| | `dotnet build` | `dotnet publish` |
|---|---|---|
| Що робить | Компілює твій код у dll/exe | Компілює + збирає ВСЕ, що треба для запуску на іншій машині |
| Куди кладе | `bin/<Config>/<Tfm>/` | `bin/<Config>/<Tfm>/[<RID>/]publish/` |
| Що в папці | Твоя dll + мінімум | Твоя dll + NuGet-залежності + (опційно) рантайм + content-файли |
| Коли | Під час розробки, для `dotnet run` | Коли треба віддати іншій машині |

Тобто `build` — «скомпілюй, щоб я міг запустити тут». `publish` — «збери все так, щоб можна було зіпнути архів і дати комусь».

## Чотири перпендикулярні виміри публікації

### 1. Debug vs Release
`-c Debug` чи `-c Release`.
- **Debug** — повільніший код, купа дебаг-символів, оптимізації вимкнені.
- **Release** — швидкий, оптимізований, менший.

Для роздачі — завжди `Release`.

### 2. Framework-dependent vs Self-contained

- **Framework-dependent** (`--self-contained false`) — вихід містить тільки твій код і залежності. Для запуску користувач повинен мати встановлений .NET Desktop Runtime правильної версії. Вихід маленький (~5–10 MB).
- **Self-contained** (`--self-contained true`) — у вихід включено весь рантайм .NET. Користувач нічого не повинен ставити. Вихід великий (~70–100 MB).

Для роздачі нетехнічним користувачам — self-contained, щоб не було проблеми «потрібно спочатку встановити .NET».

### 3. Folder vs Single-file

- **Folder** (default) — купа файлів у `publish/`: твоя `App.exe` + ~200 dll'ок рантайму + appsettings.json.
- **Single-file** (`-p:PublishSingleFile=true`) — один товстий exe, у який запаковані всі dll'ки. Content-файли (appsettings.json) все одно залишаються ззовні, поряд із exe.

Під капотом single-file — архів з розпаковкою в пам'ять при старті. Користувач бачить один exe, але всередині все те саме.

### 4. Runtime Identifier (RID)
Для якої ОС і архітектури зібрано: `win-x64`, `win-arm64`, `linux-x64`, `osx-arm64`, `linux-musl-x64`...

Для self-contained RID обов'язковий, бо треба знати, рантайм якої платформи включати. Для нашого випадку — `win-x64`.

## Trimming — оптимізація розміру

**Що це.** Статичний аналіз на етапі публікації, який викидає з виходу весь код, який твій застосунок не використовує. Як linker dead-code elimination: аналізуємо граф посилань від `Main`, усе недосяжне — геть.

**Навіщо.** Self-contained містить ВЕСЬ .NET-рантайм — ~65 MB BCL (System.*). Але реально юзаєш може 5 MB. Trimming вирізає решту — 70 MB → 20–25 MB типово.

**Як вмикається:**
```xml
<PublishTrimmed>true</PublishTrimmed>
<TrimMode>partial</TrimMode>  <!-- або "full" -->
```
- `partial` — тримає тільки dll'ки, помічені як trim-friendly. Безпечно, скромніша економія.
- `full` — тримає все, включно з твоїм кодом. Агресивніше, більше ризиків.

**Пастка — reflection.** Trimmer аналізує код статично: бачить `new Foo()`, `typeof(Foo)` — залишає `Foo`. Але `Type.GetType("MyNamespace.Foo")` рядком — не знає, що `Foo` комусь потрібен, вирізає. У рантаймі `GetType` повертає `null`, бах.

Допомогти trimmer'у — атрибути `[DynamicallyAccessedMembers]`, `[DynamicDependency]`. Пишуться руками, легко забути.

**Чому WPF і trimming не дружать:**
- XAML парситься через reflection (типи і властивості знаходяться за рядковими іменами).
- Binding'и — теж рядки (`{Binding UserName}`).
- DataTemplate, стилі, resource dictionaries — усе через reflection.

До .NET 8 WPF був офіційно несумісний з trimming. У .NET 8+ є часткова підтримка, але **офіційна рекомендація Microsoft — не вмикати для WPF без хорошого тест-покриття і готовності додавати trim-анотації**.

**Правило:** для WPF-застосунку залишай `PublishTrimmed` вимкненим. Економія не вартує «кнопка не малюється, і я не знаю чому».

## ReadyToRun (R2R) — оптимізація startup

**Що це.** AOT-precompilation на етапі публікації. Нормально .NET компілює IL у нативний код через JIT (Just-In-Time) при першому виклику кожного методу в рантаймі. З R2R це робиться заздалегідь — результат вкладається в exe поряд з IL.

**Навіщо.** Швидший startup. JIT'у не треба компілювати ключові методи при запуску — вони вже у нативному вигляді.

**Як вмикається:**
```xml
<PublishReadyToRun>true</PublishReadyToRun>
```

**Ціна:**
- Розмір виходу +30–50% (бо тепер є і IL, і native).
- Довший час самої публікації.

**Коли має сенс:**
- CLI-утиліти, що запускаються десятки разів на день — великий виграш.
- Великі застосунки з довгою startup-ініціалізацією (IDE, dev-інструменти).

**Коли не має:**
- Tray-застосунки, сервіси — стартують раз за сесію, 300 мс vs 500 мс непомітно.
- Довгоживі процеси — JIT і так встигне скомпілювати все за хвилини роботи.

## Native AOT (для контексту, не для WPF)

Третій рівень — `<PublishAot>true</PublishAot>`. Повна компіляція в нативний код, без JIT, без reflection-за-рядком взагалі. Результат — exe як у C++: малий, швидкий, стартує миттєво.

Обмеження:
- **WPF не підтримується** (навіть у .NET 10).
- Reflection-heavy бібліотеки можуть не завестися.
- Код треба писати з оглядом на обмеження (trim + AOT warnings).

Добре для веб-API, CLI, cloud-функцій. Для WPF-tray-застосунку — ні.

## Матриця рішень

| Сценарій | SelfContained | SingleFile | Trim | R2R |
|---|---|---|---|---|
| Dev-команда ділиться збіркою | ❌ (fx-dep) | ❌ | ❌ | ❌ |
| MVP для демо нетехнічним | ✅ | ✅ | ❌ (WPF) | ❌ |
| CLI-утиліта для частих запусків | ✅ | ✅ | ✅ (full) | ✅ |
| Веб-сервіс у контейнері | fx-dep | ❌ | ✅ або AOT | ✅ або AOT |

## Для ScreenTranslator

`.csproj`:
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>true</SelfContained>
  <PublishSingleFile>true</PublishSingleFile>
  <!-- PublishTrimmed: ні (WPF + reflection) -->
  <!-- PublishReadyToRun: ні (tray, startup один раз) -->
</PropertyGroup>
```

Команда:
```powershell
dotnet publish -c Release
```

Результат:
```
bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/
├─ ScreenTranslator.exe   (~80 MB, single-file self-contained)
└─ appsettings.json
```

Звідси інсталятор (Inno Setup) бере файли.

## Прапорці vs `.csproj`

Усе, що через `-p:X=Y` в командному рядку, можна прописати в `.csproj` як `<X>Y</X>`. Приклади відповідності:

| Командний | .csproj |
|---|---|
| `-c Release` | `<Configuration>Release</Configuration>` (зазвичай не пишуть, передають командою) |
| `-r win-x64` | `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` |
| `--self-contained true` | `<SelfContained>true</SelfContained>` |
| `-p:PublishSingleFile=true` | `<PublishSingleFile>true</PublishSingleFile>` |
| `-p:PublishTrimmed=true` | `<PublishTrimmed>true</PublishTrimmed>` |
| `-p:PublishReadyToRun=true` | `<PublishReadyToRun>true</PublishReadyToRun>` |
| `-p:PublishAot=true` | `<PublishAot>true</PublishAot>` |

Перевага `.csproj`: зафіксовано у репозиторії, кожен `dotnet publish -c Release` одразу правильний, не треба пам'ятати прапорці.

## Content-файли при публікації

`appsettings.json`, лого, іконки тощо копіюються в `publish/`, якщо в `.csproj` є:
```xml
<ItemGroup>
  <Content Include="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

`PreserveNewest` — копіювати, тільки якщо вихідний новіший. `Always` — завжди. `Never` — ніколи.

WPF-шаблон додає `appsettings.json` з `CopyToOutputDirectory` автоматично, нічого не треба.

**Важливо:** PublishSingleFile НЕ запаковує content-файли в exe. `appsettings.json` завжди залишається окремим файлом поруч із exe — тому його можна редагувати після встановлення.

## `.pdb` — symbols-файл

**Що це.** Program Database — файл з мапуванням IL-інструкцій на вихідний C#-код (файл, рядок, стовпчик, імена локальних змінних). Формат бінарний, у .NET Core+ — так званий **portable PDB** (менший і кроссплатформенний).

**Навіщо потрібен.** Коли застосунок падає з винятком, .NET збирає stack trace. Без pdb:
```
at System.Threading.Tasks.Task.ThrowIfExceptional(Boolean includeTaskCanceledExceptions)
at ScreenTranslator.Workflow.ScreenTranslationWorkflow.<SelectAndTranslateAsync>d__5.MoveNext()
```
З pdb поряд з exe — той самий stack trace доповнюється:
```
at ScreenTranslator.Workflow.ScreenTranslationWorkflow.<SelectAndTranslateAsync>d__5.MoveNext() in C:\...\ScreenTranslationWorkflow.cs:line 47
```
Різниця — чи знаєш одразу `:line 47`, чи треба колупатися в decompiler'і або reverse-mapping'у, щоб знайти, де саме впало.

**Коли ти бачиш це в дії:**
- У логах Serilog (`FileLogging`) exception пишеться зі stack trace — рядки коду там є саме завдяки pdb.
- Debugger (F5 у VS) без pdb не покаже, де зупинитись на breakpoint — він «заблукає» в IL.
- Кришн-дампи аналізуються через pdb (`windbg`, `dotnet-dump`).

**Варіанти генерації** (властивість `DebugType`):
| Значення | Що робить |
|---|---|
| `portable` (default у .NET Core+) | Окремий `.pdb` файл поряд з dll/exe |
| `embedded` | Pdb вкладено всередину exe/dll (більше exe, менше файлів) |
| `none` | Не генерувати взагалі. Stack trace буде без рядків |
| `full` | Старий Windows-PDB формат (.NET Framework) |

**Безпека:** pdb не містить вихідного коду, лише символи й мапування. З нього не «відновити» твій код. Decompiler (ILSpy, dnSpy) і без pdb дасть читабельний C#; pdb робить його ще зручнішим (справжні імена локальних змінних). Якщо код секретний — тоді й сам застосунок треба обфускувати, не просто pdb прибирати. Див. [obfuscation.md](obfuscation.md).

**Правило:**
- **Shippable-застосунок для друзів/себе/команди, де хочеш діагностику крашів** → залиш pdb (default).
- **Public-SDK/бібліотека для масового розповсюдження** → залиш (користувачі вдячні за нормальні stack trace у себе в логах).
- **Проєкт з реально чутливим кодом** → `DebugType=none` + обфускація, в комплексі.
- **Великий размір exe критичний** → `embedded` (один файл, не два).

Для нас — залишаємо `portable` (default). `.pdb` поряд з exe в publish — очікувана поведінка.

## Див. також
- [Ізоляція процесів і IPC](process-isolation.md) — чому `%LOCALAPPDATA%` зручно для per-user install без UAC.
