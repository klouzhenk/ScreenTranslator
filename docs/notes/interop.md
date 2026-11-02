# Нотатки: Interop (P/Invoke, Win32, GDI)

## P/Invoke
Platform Invoke — механізм .NET для виклику функцій з нативних DLL (user32.dll, kernel32.dll, довільні C-бібліотеки). Описуєш C#-«відповідник» зовнішньої функції з атрибутом, а runtime або source generator бере на себе маршалінг аргументів.

## Win32 скорочення
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

## Marshalling
Конвертація даних між .NET-ним і нативним представленням при перетині managed/unmanaged бар'єра. Потрібна, бо пам'ять і типи різні:

| Що | .NET | Native (C/Win32) |
|---|---|---|
| Рядок | `System.String`, UTF-16, immutable, managed heap | `char*` (ANSI) / `wchar_t*` (UTF-16), null-term |
| `bool` | 1 байт | `BOOL` = 4 байти (`int`) |
| Пам'ять | GC може переміщати | Фіксовані адреси |
| Структури | Можуть містити reference | Flat memory layout |

Marshaller — компонент, що виділяє буфери, копіює/конвертує, pin'ить managed-пам'ять (щоб GC її не рухав під час виклику), і робить зворотнє при поверненні.

## `DllImport` vs `LibraryImport`
Два способи оголосити P/Invoke:
- **`[DllImport]`** (класика) — marshalling-код генерується в рантаймі через reflection + IL emit. Повільніший старт, не AOT-friendly, «чорна скринька».
- **`[LibraryImport]`** (сучасний, .NET 7+) — marshalling генерується **під час компіляції** через source generator. Швидше, AOT-friendly, код можна подивитись (під `obj/`).

Вимагає від класу бути `partial`, а методів без тіла — `partial` з модифікатором. У `.csproj` треба `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` — generator пише код із сирими вказівниками. Це **не** дозвіл писати unsafe вручну (хоча можна); твого коду вимога не торкається.

Для нових проєктів однозначно `LibraryImport`.

## `LibraryImports.g.cs` — звідки береться
Коли оголошуєш `partial`-метод з `[LibraryImport]` без тіла — хто його реалізує?

**Roslyn source generators** — плагіни до компілятора, які під час компіляції генерують додаткові `.cs`-файли і додають їх у проєкт. Для `LibraryImport` це `LibraryImportGenerator`. Він знаходить усі `partial`-методи з цим атрибутом і генерує **другу частину partial-класу** з реальним тілом (fixed-блоки, GCHandle, маршалінг, перевірка last-error) у файл:

```
obj/Debug/<tfm>/generated/Microsoft.Interop.LibraryImportGenerator/.../LibraryImports.g.cs
```

Суфікс **`.g.cs`** = generated, IDE показує read-only. По `F12` з виклику ведеш саме туди.

**Чому іноді «спочатку еррори, потім зникли».** IntelliSense (швидкий аналізатор) бачить `partial`-метод без тіла і одразу малює помилку. Source generator — повільніший окремий етап. Поки він працює, IntelliSense ще не знає про згенерований код. Коли generator закінчує — файл з'являється, помилки зникають. Класична гонка в VS/Rider; інколи треба зберегти ще раз або зробити Build, щоб синхронізувалося.

## GDI: screen DC vs memory DC
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

## ROP codes (Raster Operations)
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
