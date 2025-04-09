---
layout: post
title: C4 - ISeq clarity
date: 2023-04-18 00:00:00 -0500
categories: general
---




The `ISeq` analyzer is `Compiler.AnalyzeSeq`. It receives an ISeq, which will be of the form `(op ...args...)`.
It first tries to macroexpand the form.  If macroexpanding gives us back soemthing other than what we started with, it just calls `Compiler.Analyze` on that new things.  Otherwise:


- if `op` is `nil`, throw an exception
- if `op` is a `Var` or a symbol that resolves to a `Var`, and that `Var` has `:inline` metadata with an entry with correct number of arguments, invoke that entry (it should be an `IFn`) on the arguments and recursively analyze the result.  
- If `op` is a special form, call the corresponding special form parser. (See below).
- Otherwise, call the parse for `InvokeExpr` (Also see below.)


The compiler has a map from special form symbols to the parser to be used for that special form.
Here you go:

|  Special form op | Hander |
|:-----------------|:----|
| `case*` | `CaseExpr` |
| `def` | `DefExpr` |
| `deftype*` | `DefType.Parser`, contained in `NewInstanceExpr` |
| `do`    | `BodyExpr` |
| `fn*` |  `FnExpr` |
| `if` | `IfExpr` |
| `import*` | `ImportExpr` |
| `let*`  | `LetExpr` |
| `letfn*` | `LetFnExpr` |
| `loop*` |  `LetExpr` |
| `monitor-enter` | `MonitorEnterExpr` |
| `monitor-exit` | `MonitorExitExpr` |
| `new` | `NewExpr` |
| `quote` | `ConstantExpr` |
| `recur` | `RecurExpr` |
| `reify*` |  `Reify.Parser`, contained in `NewInstanceExpr` |
| `set!` | `AssignExpr` |
| `throw` | `ThrowExpr` |
| `try` | `TryExpr` |
| `var` | `TheVarExpr` |
| `.` | `HostExpr` |

Some of the op names have an asterisk at the end.
These are the primitive forms that more advanced syntactic constructs macroexpand into.
For example, `let` has a lot of special handling for deconstructing arguments.
A `let` form will macroexpand into a `let*` that has only simple bindings.  E.g.

```clojure
(let [[x y] (f 12)] something)
```

macroexpands to 

```clojure
(let*
 [vec__24820 (f 12)
  x          (clojure.core/nth vec__24820 0 nil)
  y          (clojure.core/nth vec__24820 1 nil) ]
 something)
 ```

Also, some operators you are unlikely to type directly.  More commonly they come from reader macros, e.g.,

- `'x`  => `(quote x)`
- `#'x` => `(var x)`


### The invocation parser   

The catch-all parser at the end of `AnalyzeSeq` is `InvokeExpr.Parser.Parse`.  It might not return an `InvokeExpr`.
It could alternatively return a `KeywordInvokeExpr` or a `StaticInvokeExpr`.  
I discussed static invocation in another blog post, [The function of naming; the naming of functions]({{site.baseurl}}{% post_url 2025-02-28-function-naming}).


## Conclusion

There are many devils hidden in the details of the many parsers mentioned above.  There is no substitute for actually looking at each one in turn to understand their peculiarities.    I hope the organization presented here makes that taks less daunting.  In addition, subsequent blog posts will provide overviews of some of the more complex pieces, such as function management and interop.

