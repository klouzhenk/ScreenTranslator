# План пост-MVP рефакторингу

MVP завершений: усі 8 функціональних вимог з `CLAUDE.md` виконані. Застосунок працює, але внутрішня структура «брудна» — усе зросло з макету в `App.xaml.cs` і не було розкладено по шарах. Ціль цього документа — зафіксувати, що саме варто підчистити, в якому порядку і чому.

Правило порядку: **від найменшого до найбільшого**, щоб на кожному кроці застосунок запускався і happy path працював.

---

## 1. Винести спільний пайплайн у приватний метод

**Проблема.** `OnHotkeyPressed` і `OnRepeatLastPressed` обидва роблять:

```
cropped → RecognizeAsync → IsNullOrWhiteSpace? → ShowError("Текст не розпізнано"); return
                        ↓
                        TranslateAsync (у try/catch з TranslationException)
                        ↓
                        ShowTranslation(…)
```

Різні тільки «звідки взявся `cropped`» і «де showим». Логіка однакова, але скопіпащена.

**Що зробити.** Приватний `App` або (краще) окремий метод:

```csharp
private static async Task ProcessRegionAsync(
    BitmapSource cropped,
    Int32Rect selectionScreenRect,
    Int32Rect monitorBounds,
    IOcrService ocr,
    ITranslationService translator,
    ResultWindowOptions resultOptions)
{
    var text = await ocr.RecognizeAsync(cropped);
    if (string.IsNullOrWhiteSpace(text))
    {
        ResultWindow.ShowError(null, "Текст не розпізнано", resultOptions, selectionScreenRect, monitorBounds);
        return;
    }

    try
    {
        var translated = await translator.TranslateAsync(text);
        ResultWindow.ShowTranslation(text, translated, resultOptions, selectionScreenRect, monitorBounds);
    }
    catch (TranslationException ex)
    {
        ResultWindow.ShowError(text, FormatTranslationError(ex.Kind), resultOptions, selectionScreenRect, monitorBounds);
    }
}
```

Обидва обробники тепер стають 10-15 рядків: підготувати `cropped` + `selectionScreenRect` + `monitorBounds` → викликати `ProcessRegionAsync`.

---

## 2. Прибрати передачу сервісів через лямбди

**Проблема.** Сервіси берутся з DI в `OnStartup`, потім проштовхуються через `() => _ = OnHotkeyPressed(captureService, monitorService, ocrService, translationService, resultWindowOptions)`. Довгі списки параметрів, не можна додати залежність без зміни обох реєстрацій.

**Варіант A (легкий).** Зберегти сервіси в приватних полях `App`:

```csharp
private IScreenCaptureService _capture = null!;
private IOcrService _ocr = null!;
// ...
```

У `OnStartup` присвоїти. У `OnHotkeyPressed`/`OnRepeatLastPressed` використовувати поля замість параметрів. Лямбда реєстрації стає `() => _ = OnHotkeyPressed();`.

**Варіант B (чистий).** Окремий координатор `IScreenTranslationWorkflow`:

```csharp
public interface IScreenTranslationWorkflow
{
    Task SelectAndTranslateAsync();
    Task RepeatLastAsync();
}
```

Усі залежності — в ctor через DI. `App` тільки реєструє хоткеї:

```csharp
var workflow = _host.Services.GetRequiredService<IScreenTranslationWorkflow>();
_selectHotkey = hotkeys.Register(opts.Select, () => _ = workflow.SelectAndTranslateAsync());
_repeatHotkey = hotkeys.Register(opts.RepeatLast, () => _ = workflow.RepeatLastAsync());
```

Юля хотіла саме варіант B.

---

## 3. Винести `_lastSelectedRegion` у окремий сервіс

**Проблема.** Стан (`Int32Rect` останньої області + `Int32Rect` монітора) живе в полі `App`. Якщо координатор (крок 2) буде окремим класом — стан має жити з ним або поряд.

**Що зробити.** Малий singleton:

```csharp
public interface ILastRegionStore
{
    (Int32Rect SelectionScreenRect, Int32Rect MonitorBounds)? Current { get; set; }
}

internal sealed class InMemoryLastRegionStore : ILastRegionStore
{
    public (Int32Rect SelectionScreenRect, Int32Rect MonitorBounds)? Current { get; set; }
}
```

Пізніше легко додати персистентність (запис в file / registry між запусками) — просто ще одна реалізація.

---

## 4. Extension methods для реєстрації провайдерів

**Проблема.** В `App.OnStartup`:

```csharp
builder.Services.AddSingleton(new DeepLClient(deepLKey));
builder.Services.AddSingleton<ITranslationService, DeepLTranslationService>();
```

Composition root знає про `DeepLClient`. Технічно це легально (DIP не порушується), але неакуратно: якщо завтра додати Google — буде ще два рядки конкретики тут же.

**Що зробити.** У `Translation/`:

```csharp
public static class TranslationServiceCollectionExtensions
{
    public static IServiceCollection AddDeepLTranslation(this IServiceCollection services, string apiKey)
    {
        services.AddSingleton(new DeepLClient(apiKey));
        services.AddSingleton<ITranslationService, DeepLTranslationService>();
        return services;
    }
}
```

У `App`:

```csharp
builder.Services.AddDeepLTranslation(deepLKey);
```

`using DeepL;` зникне з `App.xaml.cs`.

Таку ж обгортку варто зробити для `Hotkeys`, `Capture`, `Ocr` — кожен шар сам реєструє свої залежності. `App` стає тонким.

---

## 5. Логування

**Проблема.** У `catch (TranslationException)` і catch-и hotkey-сервісу є `_logger`, але в `App` жодного логера. При незрозумілій поведінці — порожньо.

**Що зробити.** Прокинути `ILogger<ScreenTranslationWorkflow>` (або `ILogger<App>`, якщо залишаєш у `App`), логувати:
- початок/кінець кожної Select/RepeatLast операції (Debug);
- порожній OCR (Information з розмірами області);
- `TranslationException` + inner (Warning з Kind);
- RepeatLast без збереженої області (Information).

Коли дійде до релізу — додати файловий логер (Serilog / built-in file provider).

---

## 6. Toast «ще немає області» у RepeatLast

**Проблема.** Якщо `_lastSelectedRegion is null` (ще не було жодного Select), `OnRepeatLastPressed` мовчки повертається. Користувач не розуміє, чи взагалі хоткей спрацював.

**Що зробити.** Невелике повідомлення — наприклад, через `H.NotifyIcon` tray balloon (`_trayIcon.ShowBalloonTip("ScreenTranslator", "Спочатку виділи область (Ctrl+Alt+T)", BalloonIcon.Info)`). Без вікна, щоб гра не губила фокус.

---

## 7. Nullability pass

Перевірити, чи ввімкнено `<Nullable>enable</Nullable>` в `.csproj`. Якщо так — пройтися по warnings і прибрати залишки `!`-операторів і `null!`-ініціалізацій, залишивши тільки там, де вони справді потрібні (поля, що гарантовано ініціалізуються в `OnStartup`).

---

## Що НЕ робити в межах рефакторингу

Все з «Поза межами MVP» у `CLAUDE.md`:

- словничок / експорт у KnowBase,
- інші провайдери перекладу/OCR,
- автозапуск, UI налаштувань,
- Blazor-вікно,
- редагування OCR-рядка.

Рефакторинг — тільки реструктуризація поточного коду. Нові фічі — окремими сесіями, після закриття цього файла.
