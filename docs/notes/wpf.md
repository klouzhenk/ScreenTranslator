# Нотатки: WPF / XAML

## Що таке XAML
XML-діалект для опису UI і налаштувань WPF-застосунку. Кожен `.xaml` має «брата» `.xaml.cs` (code-behind) — разом вони одна `partial class`: XAML описує структуру, C# — поведінку.

## Структура `App.xaml`
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

## XAML namespaces і `x:`-префікс
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

## XAML attributes = C# properties
Будь-який атрибут без префікса — це присвоєння `public`-властивості. `Title="Preview"` → `window.Title = "Preview"`. `Background="Black"` → через TypeConverter рядок конвертується в `SolidColorBrush`. Властивості наслідуються по ланцюжку `Window` → `ContentControl` → `Control` → `FrameworkElement` → `UIElement` → `Visual` → `DependencyObject` — все звідти доступне.

## Content model
`ContentControl` (напр., `Window`, `Button`) має властивість `Content` типу `object`. Все, що між `<Window>` і `</Window>` в XAML, автоматично йде в `Content`. Але це **один** елемент — тому як обгортку ставлять `Panel`-контейнер (`Grid`, `StackPanel`, `DockPanel`), який тримає **колекцію** дітей через `Children`.

## Dependency properties — чому «state changed» не потрібен
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

## Hit testing і `IsHitTestVisible`
WPF визначає «на який елемент потрапила мишка» через **hit test**: іде зверху донизу по Z-order, перший непрозорий елемент під точкою миші виграє подію.

`IsHitTestVisible="False"` — елемент видимий, але **невидимий для hit-test**. Клік/рух миші проходить крізь нього на те, що під ним. Корисно для декоративних overlay-ів, які не мають їсти події.

Пастка з прозорими фонами:

| Налаштування | Подія ловиться? | Видимо? |
|---|---|---|
| `IsHitTestVisible="True"` (default) + будь-який `Fill` | Так | Так |
| `IsHitTestVisible="False"` | Ні | Так |
| `Visibility="Collapsed"` | Ні | Ні, не займає місця в layout |
| `Visibility="Hidden"` | Ні | Ні, але займає місце |
| `Fill="{x:Null}"` / без фону | Ні — hit test пропускає прозорі зони | Форми (Stroke) видно |
| `Fill="Transparent"` | **Так** | Нічого не видно, але клік ловиться |

Остання пара — класичний WPF-трюк: `Background="Transparent"` на Panel/Canvas робить порожню зону кликабельною, не псуючи вигляд. «Без фону» (null) не кликабельне.

## Attached properties: `Canvas.SetLeft`, `Grid.SetRow`
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
