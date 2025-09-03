---
layout: post
title: C4 - ISeq clarity
date: 2023-04-18 00:00:00 -0500
categories: general
---




The `ISeq` analyzer is `Compiler.AnalyzeSeq`. It receives an `ISeq`, which will be of the form `(op ...args...)`.
And we know that `op` is not symbol whose name starts with "def".   Whatever.


It first tries to macroexpand the form.  If macroexpanding gives us back soemthing other than what we started with, it just calls `Compiler.Analyze` on that new thing.  Otherwise:


- if `op` is `nil`, throw an exception
- if `op` is a `Var` or a symbol that resolves to a `Var`, and that `Var` has `:inline` metadata with an entry with correct number of arguments, invoke that entry (it should be an `IFn`) on the arguments and recursively analyze the result.  
- If `op` is a special form, call the corresponding special form parser. (See below).
- Otherwise, call the parser for `InvokeExpr` (Also see below.)


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

The catch-all parser at the end of `AnalyzeSeq` is `InvokeExpr.Parser.Parse`.  When called, we know the form to analyze looks like `(f arg1 arg2 ...)` and we know `f` is not special form symbol, as detailed above.. It might not be a symbol at all; we could have a form such as `((fn [x] (inc (* 2 x)))  y)`.  This parser does a lot of special-case analysis to determine the best type of AST node to compute.

The first step is to call `Compiler.Analyze` on `f`.   Call the resulting AST node `fexpr`.
The following special cases are handled:

- `instance?`.    There is a special type of AST node just for this case:  `InstanceOfExpr`.  (I don't know it gets its own node type.)   The conditions for this are:
    - `fexpr` is a `VarExpr`
    - the `Var` is actually `#'instance?`
    - the form has exactly two arguments.  
    
    

- static invocation.  The type of AST node to create is `StaticInvokeExpr` The conditions are:
    `fexpr` is a `VarExpr`
    - the `:direct-linking` compiler option is set to true
    - we are not in an 'evaluation context' (more on that some other day).
    - the `Var` is not marked as dynamic, does not have metatdata `:redef` = true, and does not have metadata ':declared' = true
    - The Var is bound to a class that has an `invokeStatic` method with a matching number of arguments
    I discussed static invocation in another blog post, [The function of naming; the naming of functions]({{site.baseurl}}{% post_url 2025-02-28-function-naming}).  It also will be discussed in [C4: Functional anatomy]({{site.baseurl}}{% post_url 2025-04-19-functional-anatomy}).

- primitive invocation.  We create an AST node of type `InstanceMethodExpr` to invoke the `.invokePrim` method of the function.  The conditions are:
    - `fexpr` is a `VarExpr`
    - the `Var` is bound to a class that has an `invokePrim` method with a matching number of arguments  (determined by looking at the `:arglists` metadata on the `Var`)
    - we are not in an 'evaluation context' (more on that some other day).
    We will discuss this in more detail in [C4: Functional anatomy]({{site.baseurl}}{% post_url 2025-04-19-functional-anatomy}).

- keyword invocation.  When our form looks like `(:keyword coll)`, we create an AST node of type `KeywordInvokeExpr`.  The conditions are:
    - `fexpr` is a `KeywordExpr`
    - the form has exactly one argument

- passthrough of `StaticFieldExpr` and `StaticPropertyExpr`.  This is to deal with the so-called "static field bug that replaces a reference in parens with the field itself rather than trying to invoke the value in the field."  Think of it as dealing with `(Int64/MaxValue)` when you should be writing just `Int64/MaxValue`.  

- Dealing with `QualifiedMethodExpr`.





## Conclusion

There are many devils hidden in the details of the many parsers mentioned above.  There is no substitute for actually looking at each one in turn to understand their peculiarities.    I hope the organization presented here makes that taks less daunting.  In addition, subsequent blog posts will provide overviews of some of the more complex pieces, such as function management and interop.

