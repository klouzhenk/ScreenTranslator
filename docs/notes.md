# Нотатки по ходу роботи

Пояснення концепцій, які виникали під час розробки ScreenTranslator. Короткі довідки — не підручник, а шпаргалка повернутися й пригадати. Рішення (чому обрали X, а не Y) — у `decisions.md`.

Розбито на тематичні файли:

- [Git](notes/git.md) — `git branch -M`, базові операції.
- [.NET](notes/dotnet.md) — TargetFramework, WinRT, Generic Host, `IOptions<T>`, `TaskCompletionSource`, синхронізація (`Interlocked`/`SemaphoreSlim`/`lock`/`Mutex`), `class` vs `record` vs `struct`, Windows OCR language packs, AssemblyInfo, `WinExe`.
- [WPF / XAML](notes/wpf.md) — структура `App.xaml`, `x:`-префікс, dependency properties, hit testing, attached properties, content model.
- [Interop](notes/interop.md) — P/Invoke, Win32 скорочення, marshalling, `DllImport` vs `LibraryImport`, GDI (screen vs memory DC), ROP codes.
- [Windows](notes/windows.md) — system tray / `H.NotifyIcon.Wpf`, Application manifest, `<supportedOS>` GUID-и.
