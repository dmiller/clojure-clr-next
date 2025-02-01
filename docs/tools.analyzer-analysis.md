# Analyzing tools.analyzer

The internals of tools.analyzer and tools.analyzer.clr.

## The 10K-meter view

tools.analyzer is a library for analyzing Clojure code. It produces an AST from a Clojure form.
tools.analyzer provides the basic structure and much of code for doing the analysis.  It provides extension points to allow customization for different Clojure dialects.  The extenion libraries I know of are tools.analyzer.jvm and tools.analyzer.clr.  Obviously, I'll consider the latter.
(There is also a tools.analyzer.js, but that was abandoned a number of years go.  ClojureScript has another library to replace tools.analyzer itself.)

The entry point is `analyze`:

> "Given a form to analyze and an environment, a map containing:
>  * :locals     a map from binding symbol to AST of the binding value
>  * :context    a keyword describing the form's context from the :ctx/* hierarchy.
>   ** :ctx/expr      the form is an expression: its value is used
>   ** :ctx/return    the form is an expression in return position, derives :ctx/expr
>   ** :ctx/statement the value of the form is not used
>  * :ns         a symbol representing the current namespace of the form to be analyzed
>
>   returns an AST for that form.
>
>   Every node in the AST is a map that is *guaranteed* to have the following keys:
>   * :op   a keyword describing the AST node
>   * :form the form represented by the AST node
>   * :env  the environment map of the AST node
>
>   Additionally if the AST node contains sub-nodes, it is guaranteed to have:
>   * :children a vector of the keys of the AST node mapping to the sub-nodes,
>               ordered, when that makes sense
>
>   It is considered a node either the top-level node (marked with :top-level true)
>   or a node that can be reached via :children; if a node contains a node-like
>   map that is not reachable by :children, there's no guarantee that such a map
>   will contain the guaranteed keys."

The extension points are the methods set on the dynamic variables

- `macroexpand-1` --  "If form represents a macro form, returns its expansion,           else returns form."
- `parse` --  "Multimethod that dispatches on op, should default to -parse"
- `create-var` -- "Creates a var for sym and returns it"
- `var?` --  "Returns true if obj represent a var form as returned by create-var"

These extension points have definitions in tools.analyzer.clr.  More on those later.

`analyze` calls `analyze-form` and adds `:top-level true` to the AST returned. `analyze-form` is a dynamic `Var`, but starts out bound to `-analyze-form`.  tools.analyzer.clr renames `analyze` to `-analyze`  and defines its own `tools.analyzer.clr/analyze`.  This version essentially provides dynamic bindings for the extension points and few other Vars, (notably `*ns*`) then calls the original `analyze`, followed by processing passes. (To be described.)


## Parsing

If you track through all that, `tools.analyzer.clr/analyze` sets up some variables and eventually ends up with a call to `tools.analyzer/analyze-form`.  `analyze-form` is a multimethod that discriminates on the type of its argument, with the following method mappings:

- `Symbol` --> `analyze-symbol`
- `IPersistentVector` --> `analyze-vector`
- `IPersistentMap` --> `analyze-map`
- `IPersistentSet` --> `analyze-set`
- `ISeq ` --> `analyze-seq` if `seq` is not `nil`, else `analyze-const` (which will generate a `:const` node for `nil`)
- `IType` --> `analyze-const` with `type` set to `:type`
- `IRecord` --> `analyze-const` with `type` set to `:record`
- default -> `analyze-const`

`IType` indicates an object of a type defined by `deftype`.  `IRecord` indicates an object of a type defined by `defrecord`.  Since these types also support `IPersistentMap` and other interfaces, there are `prefer-method` calls to make sure that these `deftype` and `defrecord` interpretations take precedence.

So far none of the extension points have entered into the equation. Our first encounter will be in `analyze-seq`.  Here, macroexpansion is done on the form being analyzed (the `macroexpand-1` extension point).  If the macroexpansion results in a change, we call `analyze` recursively on the result.  But if there is no change, it calls `parse` (another extension point) on the form to do special-form analysis.

tools.analyzer.clr defines its `parse` as a case statement that defaults to tools.analyzer's `-parse`, which is also a case statement.  Combined, we end up with the following mapping from the first element of the form being analyzed to the method to be called to analyze the form.  Since these mappings are all of the form  _op_ -> `parse-`_op_, we just list the ops.

|     |     |     |     |     |
|:---:|:---:|:---:|:---:|:---:|
| `.` (`dot`) | `case*`         | `def`          | `deftype*` |`do`     |
| `fn*`       | `if`            | `import*`      | `let*`     |`letfn*` | 
| `loop*`     | `monitor-enter` | `monitor-exit` | `new`      |`quote`  |
| `recur`     | `reify*`        | `set!`         | `throw`    | `try`   |
| `var`       |        

The default is `parse-invoke`.  In other words, we have a form that looks like `(op ...args...)` and the `op` is not a special form, treat it as a function call. 

A note on the names ending in `*`. Consider `let*`.  In your Clojure coding, you probably use `let` to introduce locally-bound names.  Actually, `let` is defined in as a macro that handles all kinds of special processing, such as destructuring.  The `let` ultimately expands into a `let*` form.  `let*` is only set up to handle simple binding.  Thus the form

```Clojure
(let [[x y :as my-point] (f) ] 
   (+ x y) )
```

macroexpands to 

```Clojure
(let* [vec__24813  (f) 
       x  (clojure.core/nth vec__24813 0 nil)
       y  (clojure.core/nth vec__24813 1 nil)
       my-point  vec__24813]
  (+ x y))
  ```
In our analysis, we only have to handle the case of simple bindings.
Similar considerations apply to the other `*` forms.

## The parsing environment

The various analyzers and parsers mentioned above all take the form to be analyzed/parsed and an environment.  The environment is a map that contains the following keys:

* `:locals` --     a map from binding symbol to AST of the binding value
* `:context` --   a keyword describing the form's context from the :ctx/* hierarchy.
    * `:ctx/expr`  --    the form is an expression: its value is used
    * `:ctx/return` --   the form is an expression in return position, derives :ctx/expr
    * `:ctx/statement` -- the value of the form is not used
* `:ns` --        a symbol representing the current namespace of the form to be analyzed

Our `analyze` function defaultly passes in the empty environment:

```Clojure
(defn empty-env
  "Returns an empty env map"
  []
  {:context    :ctx/expr
   :locals     {}
   :ns         (ns-name *ns*)})
```

though certainly you could pass in a more complex environment.  For example, you could provide a pre-loaded set of local bindings.

Because the environment is a Clojure map, it is immutable.  You cannot modify it in place.  Instead, you must create a new environment with the desired changes.  For example, to add a new local binding, you would do something like:

```Clojure
  (assoc-in env [:locals name] local-binding)
```
A common operation if to change the parsing context.  For example, when analyzing 

```Clojure
(do  thing1 thing2 thing3)
```

we know the values of `thing1` and `thing2` are not used.  For those forms, the appropriate parsing context is `:ctx/statement`.  The method `ctx` is provided to make this change:

```Clojure
(ctx env :ctx/statement)
```
creates a new context with `:context` set to `:ctx/statement`.  The `:locals` and other values are unchanged.  The `ctx` method is defined as:

```Clojure
(defn ctx
  "Returns a copy of the passed environment with :context set to ctx"
  [env ctx]
  (assoc env :context ctx))
  ```

  Occasionally other key-value pairs are added to the environment.  For example, `:in-try true` is added when analyzing a form that is within a `try` block. (And that has to be set back to false when analyzing the `catch` and `finally` blocks.)  But those enhancements are few in number.

### Side note: The glory of immutability

  Having the value of the `:locals` field be an `IPersistentMap` is a great solution to the problem of maintaining symbol tables.  When we parse a form, the local name bindings are passed in the environment.  Those binding are available for parsing.  We can put the map value in the AST node itself to be used in later passes.  And it can't be changed.  Passing a modified set of locals to sub-nodes is trivial and without side-effects.  If you have ever had to deal with procedural context pushing/popping and other mechanisms in doing syntactic and semantic analysis, you should appreciate the ease of this approach.

  This way of managing environments is, to my mind, better than the approach used in the Clojure compiler itself.  The compiler does use immutable maps, but manages them in a side-effectful way.   Rather than passing state around, state is maintained in a mob of `Var`s that are dynamically bound. When a new boundary for local binding is entered, such entering  `let` scope, the equivalent of `binding` is done on the `Var` holding local bindings, creating a new binding on the thread with the current value of the `Var`.  As one progresses through the `let` form's binding clauses, that `Var` has `var-set` called on it repeatedly.  When we are done processing the `let` form the `Var` binding will be popped.  (I'm pretty sure this approach to handling analysis context was one of the things Rich Hickey was referring to when he said he would do things differently if he were to implement the compiler again.)

## The AST

The AST produced by tools.analyzer is a tree of maps. 
In the docstring for `analyze`, we are given the minimum conditions an AST node

>   Every node in the AST is a map that is *guaranteed* to have the following keys:
>   * `:op`   a keyword describing the AST node
>   * `:form` the form represented by the AST node
>   * `:env`  the environment map of the AST node
>
>   Additionally if the AST node contains sub-nodes, it is guaranteed to have:
>   * `:children` a vector of the keys of the AST node mapping to the sub-nodes,
>               ordered, when that makes sense

The rest of the data in the AST node is specific to the particular `:op` of the node.

We might as well dig into these structures.  We will  not mention the `:form`,  `:env`. and `:children` keys.  Also, there are places where source code information is stored -- also ignored here.


|  `:op`  | key | description |
|:-------:|:---:|:------------|
| `:monitor-enter` | `:target` | an expression for the object to be locked |
| `:monitor-exit` | `:target` | an expression for the object to be unlocked |
| `:import` | `:class` | the actual value passed to the import form (not an expression) |

Just as a check of your understanding:  an `:op :monitor-enter` node with have `:children [:target]`, while an `:op :import` node does not have value for `:children` -- it does not contain any subexpressions, just a literal value.

Also fairly straightforward are the `:op`s coming from the various data structure types:  sets, maps, and vectors.

| `:op`   | key | description |
|:-------:|:---:|:------------|
| `:vector` | `:items` | a sequence of AST nodes representing the elements of the vector |
| `:set` | `:items` | a sequence of AST nodes representing the elements of the se |
| `:map` | `:keys` | a sequence of AST nodes representing the keys of the map. |
|        | `:vals` | a sequence of AST nodes representing the values of the map.  The two sequences are the same length. |

There are some cute optimizations in later passes.  For example, if all of the items or keys and values in one of these nodes happen to be literal values (constants or quoted forms) and there's no metadata to attach, the node can be replace by a `:const` node with the literal value.  This is a good example of the kind of optimization that can be done in later passes.


|  `:op`  | key | description |
|:-------:|:---:|:------------|
| `:do` | `:statements` | a sequence of AST nodes representing every form in the `do` block  _other than the last_. These are analyzed in context `:ctx/statement`.  |
|       |  `:ret` | the AST node representing the last form in the `do` block, the one whose value will be returned.  This will be using the same context as the `do` block itself. |
| `:if` | `:test` | the AST node representing the test expression. This will be analyzed in context `:ctx/expr` because we will use its value|
|       | `:then` | the AST node representing the _then_ branch.  This will be analyzed in context of the `if` form itself |
|       | `:else` | the AST node representing the _else_ branch.  This will be analyzed in context of the `if` form itself |
| `:quote` | `:expr` | the AST node representing the quoted value.  That value is analyzed via `analyze-const` as it will not be evaluated. |
|          | `:literal` | `true` --  This flag is set on `:quote` and `:const` nodes only.  Not otherwise referenced during parsing, it is available for later passes |
| `:set` | `:target` | the AST node representing the target of the assignment |
|        | `:val` | the AST node representing the value to be assigned |
| `:new` | `:class` | The ast node representing the class to be instantiated.  Note that when analyzing this, the `:locals` in the environment are set to be empty -- local bindings are not allowed to shadow class names |
|         | `:args` | a sequence of AST nodes representing the arguments to the constructor |
| `:try`  | `:body` | a sequence of AST nodes representing the forms in the `try` block |
|       | `:catches` | a sequence of AST nodes representing the forms in the `catch` block (see below) |
|       | `:finally` | an AST node representing the forms in the `finally` block (see below) -- multiple forms are wrapped in a `do`.  This key/value pair is optional |
| `:catch` | `:class` | the class of the exception to be caught.  As above, the form is analyzed with locals cleared |
|          | `:body` | an AST node representing the body, with a `do` wrapper if necessary |
|          | `:local` | the symbol representing the local binding for the caught exception. See below for more about this. |
| `:throw` | `:exception` | the AST node representing the exception to be thrown |
| `:var` | `:var` | The 

That's about it for the easy ones.  If you think about what the underlying forms look like, generating these representations should be relatively straightforward.  He says with a heavy sigh.

## Local bindings

We snuck in our first local binding in the handling of `catch` expressions.  Where can new local bindings be established?

- `catch` blocks inside a `try` expression
- `let*` and `letfn*` forms
- anywhere function bodies with parameters appear
    - `fn*` forms
    - `reify*` forms
    - `deftype*` forms

There is an AST node type that defines a local binding and there are references to local variables.  Binding nodes appear where local variables are defined, e.g., the associated value for the `:local` key in an `:op :catch` node.
Binding nodes also appear in the `:locals` map in parse contexts.

A binding node has the following keys:

| Key | Description |
|:---:|:------------|
| `:op` | `:binding` |
| `:form` | typically the symbol being bound |
| `:env` | the environment in which the binding is made |
| `:name` | the symbol being bound |
| `:init` | the AST node representing the value to initialize (optional) |
| `:local` | a value indicating where the binding originated.  Values include `:catch`,  `:letfn`, `:let`, `:loop`, `:arg`, `:fn`, `:this`,  `:field` |
| `:variadic` | `true` if this is an `:local :arg` node and the argument is variadic  (after a `&`) |
| `:arg-id` | an integer giving the position of the argument in the list of arguments. |
| `:mutable` | `true` if this is a `:local :field` node (from a `deftype*`) and the field is marked as mutable |
| `:o-tag` | the class of the `deftype*` or `reify*` being defined (used in type inferencing in later passes) |
| `:tag` | the class of the `deftype*` or `reify*` being defined (used in type inferencing in later passes) |
| `:children` | typically, this is `[:init`] if the binding has an `:init` value |

A reference to a local in the form is always a symbol.  The function `analyze-symbol` is called when a symbol is encountered in an place where its value is needed.  We'll talk more about this below.  For now, the key point is that the first thing `analyze-symbol` does is look up the symbol in the `:locals` map of the environment.  The `:locals` map symbols to bindings.  If the symbol is present in the map, that binding is retrieved and returned with some minor modifications.  Here is the relevant code:

```Clojure
(if-let [{:keys [mutable children] :as local-binding} (-> env :locals sym)] 
    (merge (dissoc local-binding :init)            
            {:op          :local
             :assignable? (boolean mutable)
             :children    (vec (remove #{:init} children))}
 ```

We retrieve the binding from the map, remove the `:init` key's value, and remove `:init` from the `:children` value if it is there.  We set `:op :local`.  The value for `:assignable?` is true if the binding is for a mutable field in a `deftype*`. 


## The forms that bind (locals)

Let us `let*` to start.  `loop*` shares analysis code with `let*` only adding a `:loop-id` value (a gensym'd symbol) to the environment.  For each binding, we create an AST node for the initializtion form and then a binding form, as outlined above, for the local binding.  On each iteration, we augment the environment with the new binding just created.
Finally, we analyze the body of the form (wrapping it in a `do`).


|  `:op`  | key | description |
|:-------:|:---:|:------------|
| `:let` or `:loop` | `:bindings` | a sequence of AST nodes representing the bindings, in order of declaration |
|                   | `:body` | the AST node representing the body of the form |
| `:loop` | `:loop-id` | the gensym'd symbol indentify the loop itself (useful when loops are nested) |

`letfn*` is similar.  There is a difference in how the binding are processed.  In `let*` and `loop*`, the binding are processed sequentially in order.  That allows code such as

```Clojure
(let [x 12
      y (+ x 1)
      x (inc x)]
  [x y])
```

to be processed properly.

With `letfn*`, mutual recursion is allowed among the functions that are being bound. From the docstring:  "All of the names are available
in all of the definitions of the functions, as well as the body."  From the examples on the doc page:

```Clojure
(letfn [(twice [x]
           (* x 2))
        (six-times [y]
           (* (twice y) 3))]
  (println "Twice 15 =" (twice 15))
  (println "Six times 15 =" (six-times 15)))
```

To accomplish this a set of binding nodes without `:init` fields are created.  The `:locals` map is augmented with these bindings.  Then we can analyze each init form in the augmented environment.  We merge the init forms into the bindings when creating the final AST node for the `letfn*`.


Then the `:init` fields are filled in with the actual function definitions.  The body of the `letfn*` is analyzed in the augmented environment.  The set of entries in the `:op :letfn` node is the same as for `let*` and `loop*.

While we're in the territory, parsing `recur` is relatively trivial.  We do check that the recur is in a tail position. (The environment `:contxt` is `:ctx/return`.)  However, mismatch the `recur` arguments and the loop locals is not performed at this time -- a later pass will handle that.

|  `:op`  | key | description |
|:-------:|:---:|:------------|
| `:recur` | `:loop-id` | the gensym'd symbol identifying the loop being recursed to |  
|          | `:exprs` | a sequence of AST nodes representing the arguments to the recur |

## Proper functioning

> We've covered a lot of ground.  We've parsed a lot of forms.  We've created a lot of AST nodes.  But we haven't done anything with them.  We haven't done any analysis.  We haven't done any optimization.  We haven't done any type inference.  We haven't done any code generation.  We haven't done any of the things that make a compiler a compiler.

Oh, wait a minute.  That's just Copilot talking.  Seems a bit pessimistic.

> We have done a lot of work.  We have a lot of information.  We have a lot of structure.  We have a lot of data.  We have a lot of potential.  We have a lot of possibilities.  We have a lot of power.

Oh, wait a minute.  That's just Copilot giving me a pep talk.  Seems a bit optimistic.

Let's try to deal with functional forms.  `fn*` is the simplest.  Similar to `let`, `fn` itself is a macro that allows destructuring args.  it will expand into a `fn*` form that has only simple arguments.

The syntax for `fn*` has several variants.  There might or might not be a name following the `fn*`.  There could be just a single arity provided or multiple arities.  So one must deal with all of

```Clojure
(fn* [args*]expr* )
(fn* name [args*]expr* )
(fn* ([args*]expr*) ...  )
(fn* name ([args*]expr*) ...  )
```
The input form is analyzed and regularized to the last syntax.  The _name_ will be will be _nil_ if not provided.  A binding is created for the _name_:

```Clojure
{:op    :binding
 :env   env
 :form  name
 :local :fn
 :name  name}
```
This will be added to the environment passed in under key `:local` when parsing the function bodies.  There are some other environment games: getting rid of the `:in-try` key if there is one, adding `:once` value of `true`/`false` depending on what the metadata of the form indicates.
Then each method is analyzed in the augmented environment.  

After some error checking, a binding is created for each argument:

```Clojure
 {:env       env
  :form      name
  :name      name
  :variadic? (and variadic?
                  (= id (dec arity)))
  :op        :binding
  :arg-id    id
  :local     :arg}
```

where the `id` values are sequential starting from 0.  The body is eventually analyzed in an augmented environment containing the argument bindings, a `:loop-id` (every function body is implicitly a loop).   The AST node for a method looks like:

```Clojure
  (merge
     {:op          :fn-method
      :form        form
      :loop-id     loop-id
      :env         env
      :variadic?   variadic?
      :params      params-expr
      :fixed-arity fixed-arity
      :body        body
      :children    [:params :body]}
     (when local
       {:local (dissoc-env local)})))
```

where `fixed-arity` is argument count, not include the variadic argument if there is one.  The merge is to mergin the `:local` binding that was passed in, if not `nil`.

Once all the method body have been parsed, additional error checking is done that looks at all of the methods together; e.g., checking that no there is no more than one variadic method, that no fixed-arith method has arity greater than the fixed-arith of the variadic method, and no two methods have the same arity.

When all that is done, we generate the AST node for the `fn*`:

```Clojure
    (merge {:op              :fn
             :env             env
             :form            form
             :variadic?       variadic?
             :max-fixed-arity max-fixed-arity
             :methods         methods-exprs
             :once            once?}
            (when n
              {:local name-expr})
```

Again, the trick of adding the `:local` binding if we have a name.

`deftype*` and `reify*` are similar to `fn*`.  The extra complications are not worth spending the time on here.

## Anaylzing symbols

When a symbol is encounted in a position where it is to be evaluated, we have to figure out what the symbol is referring to.  That is the job of `analyze-symbol`.

This function starts by macroexpanding the symbol.  If you are familiar with `clojure.core/macroexpand` and `clojure.core/macroexpand-1`, this likely is surprising; they don't do anything to symbols.  However, the assumption in tools.analyzer is that the `macroexpand-1` (that's one of the extension points) will expand a symbol of the form `TypeName/Field` into `(. TypeName Field)` -- that can be handled by the `parse-dot` function.

If it doesn't expand, as mentioned above, we look at the `:locals` in the current environment for a local binding.  We use that if found.  If not, 
we call `t.a.utils/resolve-sym`.  `resolve-sym` plays some games with a global environment that caches all namespace mappings, but basically it sees if the relevant namespace (based on the namespace portion of the symbol's identity) has a mapping for the symbol's name.
Failing that, it checks the symbol's namespace string to see if it is the name of a type.  Failing that, well, it defaults.  Here are the possible ops for the AST node:

|  `:op` value  | description |
|:-------:|:------------|
| `:local` | the symbol is a local binding |
| `:var` | the symbol maps to a var |
| `:maybe-host-form` | Something like  `System.Int64/MaxValue` |
| `:maybe-class` | Who knows?  Maybe we'll get lucky.|

Further analysis of `:maybe-host-form` and `:maybe-class` is left to later passes.


## Macroexpansion

As mentioned above, macroexpansions in the analyzer is a bit different than the macroexpansion you see in the Clojure environment.  For one thing, it works on resolving symbols.  In addition to the usual expansion of macros, it also applies inline expansions. And it 'desugars' host expressions.  In other words, if we have a sequence (rather a symbol) of the form `(op ...args...)`, we look at `op`:

- if it is one of the special operators, such as `let*`, we just return the form.
- we try to see if the `op` is a local binding and also if it resolves to `Var`.
- if it is not local and it resolve to a `Var`, we look for `:macro` metadate.  We also look for `:inline-arities` and see if has an entry that matches the number of argumentns in the form.
- if it is macro, evaluate it and return the result.
- if it is inline, evaluate the attached `:inline` function and return the result.
- otherwise, we call `desugare-host-expr` on the form.

`desugar-host-expr` does the work of seeing if we have one of these situations:

- `op` is _class/field_: we return `(. class field ...args...)`
- `op` is _.name_: we return `(. name  ...args...)`
- `op` is _name._: we return `(. new name ...args...)`



## A host of problems

Dealing with host interop brings a lot of issues.  A minimum analysis is done during the syntactic analysis phase.  A lot more work is done in later passes.

The special form triggering host interop analysis is just `.`.  The user could have just entered a `.` form.  More likely (and recommended), then used one of the special ways of designating interop outlined above. 

`parse-dot` does a bunch of error checking, then tries to figure what you were trying to call.  The resulting AST node will have one fo the following `:op` values:

|  `:op` value  | description |
|:-------:|:------------|
| `:host-call` | method call |
| `:host-field` | field access |
| `:host-call` | either field access or no-args method call |

Later analysis passes make more precise the designation, creating replacement nodes with more specific `:op` values.
The `analyze-host-expr` pass states:

  "Performing some reflection, transforms :host-interop/:host-call/:host-field
   nodes in[to] either: :static-field, :static-call, :instance-call, :instance-field
   or :host-interop nodes, and a :var or :maybe-class node in a :const :class node,
   if necessary (class literals shadow Vars).

   A :host-interop node represents either an instance-field or a no-arg instance-method. "

## Invocation

Last, but definitely not least, is `parse-invoke`.  When analyzing a form like `(op ...args..)` and macroexpansion has done nothing and `op` is not special, we assume we likely have a function invocation.  This function just parses the `op` and the `...args...` and returns an AST node with `:op :invoke`.  The `:op` node will have the following keys:

```Clojure
{:op   :invoke
            :form form
            :env  env
            :fn   fn-expr
            :args args-expr}
```

More analysis is done in later pass:

```Clojure
(defn classify-invoke
  "If the AST node is an :invoke, check the node in function position,
   * if it is a keyword, transform the node in a :keyword-invoke node;
   * if it is the clojure.core/instance? var and the first argument is a
     literal class, transform the node in a :instance? node to be inlined by
     the emitter
   * if it is a protocol function var, transform the node in a :protocol-invoke
     node
   * if it is a regular function with primitive type hints that match a
     clojure.lang.IFn$[primitive interface], transform the node in a :prim-invoke
     node"
   ...
```



## Semantic analysis

After the AST node has been created by the mechanism outlined above, additional passes are made over the AST tree to add information and possibly rewrite sections of the tree.  The platform-specific code defines a default set of passes.  One can create and use a custom set of passes.

The passes themselves are just functions that an AST and return an AST.
The will be applied by walking the tree.  The tree will be walked in a depth-first manner.  The function can be applied in 'pre' or 'post' mode.  In 'pre' mode, the function is applied to the current node before its children are processed.  In 'post' mode, the function is applied after the children are processed.  The children are taken from the `:children` key that specifies what other keys to access to get the actual AST sub-nodes to process.  They can be specified to traverse in the child keys in either the order they appear in the vector or in reverse order.

There is a sophisticated scheduling algorithm that looks at the entire set of passes that are to be applied and optimizes their application.  Each pass must provide information regarding what other passes it depends on and other information.  The scheduling algorithm will attempt to group compatible passes in an appropriate order so that there is a tree traversal per group rather than per pass.

The passes defined for in _tools.analyzer.clr_ are: 

```Clojure
#{#'warn-on-reflection
    #'warn-earmuff
    #'uniquify-locals
    #'source-info
    #'elide-meta
    #'constant-lift
    #'trim
    #'box
    #'analyze-host-expr
    #'validate-loop-locals
    #'validate
    #'infer-tag
    #'classify-invoke}
```

Note that this is a set, not a sequence.  Grouping and ordering are done by the scheduler.  Here is the metadata for these passes.  (These are all under the `:pass-info` key in the metadata for the function.)

```Clojure
warn-on-reflection     {:walk :pre  :depends #{#'validate} :after #{#'validate-loop-locals}}
uniquify-locals        {:walk :none :depends #{}}
source-info            {:walk :pre  :depends #{}}
elide-meta             {:walk :any  :depends #{} :after #{#'source-info}}
constant-lift          {:walk :post :depends #{}}
trim                   {:walk :none :depends #{} :after #{#'elide-meta}}
box                    {:walk :pre  :depends #{#'infer-tag} :after #{#'validate}}
analyze-host-expr      {:walk :post :depends #{}}
validate-loop-locals   {:walk :post :depends #{#'validate} :affects #{#'analyze-host-expr #'infer-tag #'validate} :after #{#'classify-invoke}}
validate               {:walk :post :depends #{#'infer-tag #'analyze-host-expr #'validate-recur}}
infer-tag              {:walk :post :depends #{#'annotate-tag #'annotate-host-info #'fix-case-test #'analyze-host-expr} :after #{#'trim}}
classify-invoke        {:walk :post :depends #{#'validate}}

// mentioned above but not in explicit passes

annotate-tag             {:walk :post :depends #{} :after #{#'constant-lift}}
annotate-host-info       {:walk :pre :depends #{} :after #{#'elide-meta}}
fix-case-test            {:walk :pre :depends #{#'add-binding-atom}}

add-binding-atom         {:walk :pre :depends #{#'uniquify-locals} :state (fn [] (atom {}))}
```

At this time, I am not prepared to go into detail on each of these.

