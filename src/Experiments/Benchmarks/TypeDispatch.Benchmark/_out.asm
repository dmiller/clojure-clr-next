System.IO.FileNotFoundException: Could not find file 'c:\work\clojure\clojure-clr-next\src\Experiments\Benchmarks\TypeDispatch.Benchmark\_out.asm_tmp.dll'.
File name: 'c:\work\clojure\clojure-clr-next\src\Experiments\Benchmarks\TypeDispatch.Benchmark\_out.asm_tmp.dll'
   at Microsoft.Win32.SafeHandles.SafeFileHandle.CreateFile(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.File.ReadAllBytes(String path)
   at PowerUp.Watcher.FSharpWatcher.DecompileToASM(String code, String compiledArtifactName) in C:\work\tools\PowerUp-main\src\PowerUp.Watcher\FSharpWatcher.cs:line 604
   at PowerUp.Watcher.FSharpWatcher.<>c__DisplayClass9_0.<<WatchFile>b__0>d.MoveNext() in C:\work\tools\PowerUp-main\src\PowerUp.Watcher\FSharpWatcher.cs:line 163