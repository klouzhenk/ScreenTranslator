# Нотатки по ходу роботи

Пояснення концепцій, які виникали під час розробки ScreenTranslator. Короткі довідки — не підручник, а шпаргалка повернутися й пригадати. Рішення (чому обрали X, а не Y) — у `decisions.md`.

Розбито на тематичні файли:

- [Git](notes/git.md) — `git branch -M`, базові операції.
- [.NET](notes/dotnet.md) — TargetFramework, WinRT, Generic Host, `IOptions<T>`, `TaskCompletionSource`, синхронізація (`Interlocked`/`SemaphoreSlim`/`lock`/`Mutex`), `class` vs `record` vs `struct`, Windows OCR language packs, AssemblyInfo, `WinExe`.
- [WPF / XAML](notes/wpf.md) — структура `App.xaml`, `x:`-префікс, dependency properties, hit testing, attached properties, content model.
- [Interop](notes/interop.md) — P/Invoke, Win32 скорочення, marshalling, `DllImport` vs `LibraryImport`, GDI (screen vs memory DC), ROP codes.
- [Windows](notes/windows.md) — system tray / `H.NotifyIcon.Wpf`, Application manifest, `<supportedOS>` GUID-и.
- [Архітектура](notes/architecture.md) — feature-based (vertical slice) організація коду, порівняння з Clean і N-Layered, правила і code smells.
- [Access modifiers](notes/access-modifiers.md) — усі 6 модифікаторів у C#, defaults, коли яке брати для застосунку vs бібліотеки.
- [Коди мов](notes/language-codes.md) — ISO 639-1/2/3, BCP-47, ISO 15924 writing scripts, що очікують різні API (DeepL, Windows OCR, HTML, .NET), пастка з DeepL `UK`.
- [Ізоляція процесів і IPC](notes/process-isolation.md) — чому `bool`/`lock` не бачать одне одного між процесами, що можна ділити (kernel objects, pipes, shared memory, сокети), WSL2 як «інша машина», `networkingMode=mirrored`.
- [Публікація .NET-застосунку](notes/release-publishing.md) — `build` vs `publish`, чотири виміри (Debug/Release, fx-dep/self-contained, folder/single-file, RID), trimming і чому WPF його не любить, ReadyToRun і коли він потрібен, Native AOT, `.pdb` і навіщо він.
- [Обфускація](notes/obfuscation.md) — маскування скомпільованого коду (renaming, string encryption, control flow flattening), інструменти, трейд-офи, коли варто і коли ні.
- [Inno Setup](notes/inno-setup.md) — installer-builder для Windows, синтаксис `.iss` (INI + preprocessor + Pascal), основні секції, constants, flags, autostart через Registry, альтернативи (WiX, MSIX).
- [Файли-контейнери з кількома варіантами](notes/container-files.md) — як один `.ico` обслуговує всі розмірності, споріднені формати (`.icns`, HEIC, mipmap, fonts, MKV, universal binary, ZIP-based), чому 256 — стеля для `.ico`, SVG як третій шлях.
