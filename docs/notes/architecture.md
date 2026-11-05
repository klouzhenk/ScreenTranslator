# Нотатки: Архітектура (feature-based)

## Суть

**Feature-based (vertical slice / modular)** організація коду: папка = одна фіча або технічна здатність, усе що для неї потрібне (контракт, реалізація, DTO, конфіг, помічники) живе всередині. Між папками — тонкі горизонтальні контракти через інтерфейси, і більше нічого.

Протилежність — **layered / horizontal slice**, де поділ по технічному призначенню (Presentation / Business / Data), і одна фіча «розмазана» по шарах.

## Два різновиди під одним ім'ям

| | Приклад папок | Коли |
|---|---|---|
| **Feature folders (user-facing)** | `Checkout/`, `Login/`, `UserProfile/` | Бізнес-застосунки з user-flow. Класика в ASP.NET MVC / Blazor. |
| **Module / Component folders (технічні)** | `Capture/`, `Ocr/`, `Translation/` | Утиліти, pipeline-застосунки, SDK. **Наш випадок у ScreenTranslator.** |

Обидва — під парасолькою «feature-based», просто перший про бізнес, другий про техніку.

## Наш приклад — ScreenTranslator

```
src/ScreenTranslator/
├── App.xaml.cs         # composition root — єдиний, хто знає всіх
├── Capture/            # знімок екрана + монітори
├── HotKeys/            # глобальні хоткеї
├── Ocr/                # розпізнавання тексту
├── Translation/        # DeepL + ResultWindow (UI показу перекладу)
├── Workflow/           # координатор pipeline-у + state (ILastSelectionStore)
├── Configuration/      # Options POCO
├── Logging/            # Serilog setup
└── Styles/
```

Кожна папка знає тільки свої інтерфейси. `Workflow/` — координатор — залежить тільки від абстракцій сусідніх папок (`IOcrService`, `ITranslationService`, `IScreenCaptureService`). Жодна «технічна» папка (Capture / Ocr / Translation) не знає інших. `App.xaml.cs` як composition root — єдине місце, де збираємо все разом через DI extensions кожного шару.

## Правила

- Контракт у папці — у вигляді інтерфейсу `IXxxService` + типи, які входять/виходять з нього. Публічне.
- Реалізація — `XxxService` (internal, видно тільки збірці) + `XxxServiceCollectionExtensions.AddXxx(...)` для реєстрації в DI.
- Більше з папки назовні **нічого не стирчить**. Якщо треба — витягуємо в `Shared/` або `Core/`.
- Composition root (`App.xaml.cs`) — єдиний, кому дозволено знати конкретні реалізації.

## Code smells, що пішло не так

| Симптом | Що означає |
|---|---|
| Папки хрест-навхрест залежать одна від одної (цикл) | Межі провели неправильно, треба переглянути. |
| Один тип раптом потрібен у 3-4 папках | Виносимо в `Core/` або `Shared/`. |
| Одна папка «знає все про всіх» | Нормально, якщо через інтерфейси (координатор). Погано, якщо через конкретні реалізації. |
| `using OtherFeature.Internals.*` з іншої папки | Інкапсуляція порушена; тип з іншої папки або треба зробити public, або скопіювати, або витягти у Shared. |

## Коли НЕ брати feature-based

- Велика **бізнес-логіка з доменною моделлю** (DDD-стиль) — тоді Clean Architecture з явним `Domain/` ядром зручніша.
- Велика команда, різні люди володіють різними шарами — N-Layered з чітким Presentation / Business / Data розподіляє відповідальність між командами.
- Потрібна строга **залежність від шарів** (компілятор має заборонити Core → Infrastructure) — feature folders не дають такої гарантії без окремих проєктів у .sln.

## Коротке порівняння

| Підхід | Поділ | Залежності | Коли виграє |
|---|---|---|---|
| **N-Layered** (horizontal) | Presentation / Business / Data | Вертикальні: Presentation → Business → Data | Традиційні CRUD-застосунки, велика бізнес-логіка. |
| **Clean / Hexagonal** | Domain → Application → Infrastructure → Presentation | Тільки всередину, до Domain | Багата доменна модель, use cases, DDD. |
| **Feature-based** (наш) | По фічах або технічних здатностях | Горизонтальні, через інтерфейси | Pipeline-утиліти, малі-середні застосунки, кожна фіча самодостатня. |

## Що почитати

- **Scott Sauber** — https://scottsauber.com/2016/04/25/feature-folder-structure-in-asp-net-core/ — найкоротший старт (10 хв).
- **Jimmy Bogard** — https://www.jimmybogard.com/vertical-slice-architecture/ — глибше, він автор MediatR і головний пропагандист.
- **Microsoft** — https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures — порівняння стилів у контексті .NET.
