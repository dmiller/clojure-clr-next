---
layout: post
title: C4 - A time for reflection
date: 2025-09-14 00:00:00 -0500
categories: general
---

Sometimes you can't avoid reflection.  We discuss how Clojure(JVM/CLR) try to avoid reflection and how they handle reflection that can't be avoided.  ClojureCLR differs significantly from ClojureJVM in using the services of the Dynamic Language Runtime (DLR) to handle reflection.  As a bonus, we also discuss how C# implements `dynamic`.

Given a host expression, we analyze it to determine if we can resolve the exact type and specific member (method, property, field, constructor) to be invoked.  If we can, then there is no need for reflection.  We can compile the host expression to a direct call to the member, possibly with type casts on arguments and the return value to ensure conformance to the member's signature.  If we cannot determine the exact type+member, we need to use compile to code to that uses reflection to determine the member to be invoked at runtime.

If you need a refresher on host interop expressions, refer to [Java interop](https://clojure.org/reference/java_interop).  For ClojureCLR extensions to host interop, take a look at

- [Basic CLR interop](https://github.com/clojure/clojure-clr/wiki/Basic-CLR-interop)  -- not much here.
- [Calling generic methods](https://github.com/clojure/clojure-clr/wiki/Calling-generic-methods)
- [ByRef and params](https://github.com/clojure/clojure-clr/wiki/ByRef-and-params)


## Analysis of host expressions

Look at [Java interop: Member access](https://clojure.org/reference/java_interop#_member_access) for a description of the various host interop expressions.  

| Expression | Macroexpansion | AST node | QME |
|------------|-------------|----------|----|
| `(.instanceMember instance args*)` | `(. instance instanceMember args*)` |  |  |
| `(.instanceMember Classname args*)` |  `(. (identity Classname) instanceMember args*)` |  |  |
| `(.-instanceField instance)` | `(. instance -instanceField)` |  |  |
| `(Classname/staticMethod args*)` |  | `StaticMethodExpr` |  Y |
| `(Classname/.instanceMethod instance args*)` |  | `InstanceMethodExpr` |   Y |
| `Classname/staticField` |  | `StaticFieldOrPropertyExpr` |  Y |

For constructors (see [Java interop: The Dot special form](https://clojure.org/reference/java_interop#_the_dot_special_form)), we have:

| Expression | Macroexpansion | AST node |  QME |
|------------|-------------|----------|---|
| `(Classname. args*)` | `(new Classname args)`  | |
| `(Classname/new args*)` | |  `NewExpr` |  Y | 
| `(new Classname args*)` | |  `NewExpr` |  |


Several of the forms are expanded to expressions involving the dot (`.`) special form; that special form is handed by the `HostExpr` parser.  The classic `(Classname. args*)` also macroexpands to `(new Classname args)`, which is handled by the `NewExpr` parser.

The new 'qualified' forms, AKA qualified method expressions or QMEs, were introduced in Clojure 1.12.
Except for `Classname/staticMethod`, which had been handled;  in 1.12, it got moved to the QME-handling code.  An expression of type `QualifiedMethodExpr` is created by `Compiler.AnalyzeSymbol` when it runs into `Classname/staticMethod` or `Classname/.instanceMethod` or `Classname/new` forms.  
These are then seen as the `(QME args)` in the `InvokeExpr` parser, which will directly create the desired AST node rather than going through the `HostExpr` parser as we do with the others.

Another way of presenting the information above is:

| Expression | AST node | QME |
|------------|----------|----|
| `(.instanceMember instance args*)` <br/>  `(.instanceMember Classname args*)` <br/>  `(.-instanceField instance)`  | macroexpands to `.` special form | handled by `HostExpr` parser |
| `Classname/staticField` | detected by `AnalyzeSymbol` |  will result in either QME or `FieldOrPropertyExpr`|
|  `(Classname/staticMethod args*)` <br/> `(Classname/.instanceMethod instance args*)` <br/> `(Classname/new args*)`| `AnalyzeSymbol` create QME | handled by `InvokeExpr` parser |
| `(new Classname args*)` | already a special form | handled by `NewExpr` parser|  |

## The `.` special form

The forms listed above are the preferred expressions in user code.  One should only write the `.` special form directly in macro definitions.  However, in the compiler, macroexpansion yields `.` special forms, and that becomes the locus of analysis.

[Java interop: The Dot special form](https://clojure.org/reference/java_interop#_the_dot_special_form) give us the list of syntacic expressions that must be parsed.

- `(. instance-expr member-symbol)`
- `(. Classname-symbol member-symbol)`
- `(. instance-expr -field-symbol)`
- `(. instance-expr (method-symbol args*))` or `(. instance-expr method-symbol args*)`
- `(. Classname-symbol (method-symbol args*))` or `(. Classname-symbol method-symbol args*)`

These forms are handled by the `HostExpr` parser. `HostExpr` is an abstract class;  it will generate an instance of one its concrete subclasses.  

### HostExpr parsing on the JVM

The first significant step is to determine if we are dealing with an static or an instance member.  Noting that the first element after the `.` is either an expression or a class name symbol,  we determine which it is:

```Java
Class c = maybeClass(RT.second(form), false);
//at this point c will be non-null if static
Expr instance = null;
if(c == null)
    instance = analyze(context == C.EVAL ? context : C.EXPRESSION, RT.second(form));
```

At this point, if `c` is non-null, we are dealing with a static member;  otherwise, we have an instance member, and `instance` holds the AST node for the target expression.

Next we try to determine if we are looking at a field access or a method call.  The first requirement is that our form looks like `(. target symbol)`:

```Java
boolean maybeField = RT.length(form) == 3 && (RT.third(form) instanceof Symbol);
```

We then check if there is a zero-arity method of the given name -- unless we have an instance call and the name starts with`-`, in which case we are definitely dealing with a field access.  We still might be a field if there no zero-arity methods.

```Java
if(maybeField && !(((Symbol)RT.third(form)).name.charAt(0) == '-'))
    {
    Symbol sym = (Symbol) RT.third(form);
    if(c != null)
        maybeField = Reflector.getMethods(c, 0, munge(sym.name), true).size() == 0;
    else if(instance != null && instance.hasJavaClass() && instance.getJavaClass() != null)
        maybeField = Reflector.getMethods(instance.getJavaClass(), 0, munge(sym.name), false).size() == 0;
    }
```

What happens next determines if `maybeField` is true or not.
If `maybeField` is true, we _might_ be looking at a field access. 
We create a `StaticFieldExpr` node if we are in the static case (`c` is non-null.)
We create an `InstanceFieldExpr` node if we are not in the static case.
We pass a flag indicating if the name started with a `-`, indicating that we are definitely dealing with a field access.  Don't be fooled.  An `InstanceFieldExpr` can generate reflection code that can access either a method or field at runtime, depending on what the runtime type of the `instance` expression turns out to be.  See below.

If `maybeField` is false, we are looking at a method call.  We create either a `StaticMethodExpr` or an `InstanceMethodExpr` node, depending on whether we are dealing with an instance or static member.

| Target is | `maybeField` | Node created |
|-----------|--------------|--------------|
| Type      | true         | `StaticFieldExpr` |
| Type      | false        | `StaticMethodExpr` |
| Not a type | true         | `InstanceFieldExpr` |
| Not a type | false        | `InstanceMethodExpr` |

The constructors for each of these node types does some additional analysis.

- `StaticMethodExpr`:
    - We have a known static type, we have a name, we have an arity.  If there are no methods of the given name and arity on the given type, we throw an error.  (Method used to do method lookup:  `Reflector.getMethods(...)`.)
    - if there is more than one method of the given name and arity, we try to resolve to the best match based on the method argument types and the types of the provided arguments. (Method used to find best match: `Compiler.getMatchingParams(...)`.)
    - if we cannot pick a best match, we will generate a reflection call during code-gen.  (Maybe) print a warning.
    - if we have a best match, we will be able to generate a direct call during code-gen.
    - if we have a direct match and *unchecked-math* is :worn-on-boxed and the method is on the list of 'boxed match' methods, (maybe) print a warning.

- `InstanceMethodExpr`:
    - if we do not know the type of our target, we will be generating reflection code during code-gen. (Maybe) print a warning.
    - if we do know the type, the process is similar to `StaticMethodExpr` -- look for methods of the given name and arity, try to find a best match, etc.  No boxing warnings, though; those methods are static only.  There is an additional step to if we find a method but its declaring class is not public: we look up the class hierarchy to see if there is a public superclass that also declares the method.  (`Reflector.getAsMethodOfPublicBase(...)` does this.)

- `StaticFieldExpr`:
    - this gets called only if there is not a zero-arity method of the given name on the given type.
    - if there is no field of the given name on the given type, we throw an error.
    - We will only generate code if there is a field of the given name on the given type, so no reflection will be needed.

- `InstanceFieldExpr`:
    - if we know the type of our target, we look for a field of the given name on that type.  
    - if the target type is unknown or we can't find a field on the target type, (maybe) print a warning; we will generate reflection code during code-gen.


### Code generation on the JVM

- `StaticFieldExpr` is the simplest.  
    - If we make it to code-gen, we know the type and the field.  We can just emit the code to access the static field.  This will never generate reflection code.

The other three node types have to decide if they can emit direct calls or if they need to emit reflection code.

- `StaticMethodExpr` 
    - direct:  Emit the arguments, possibly with casts--that's done by `MethodExpr.emitTypedArgs(...)`.  Call the method.
    - reflection: set up a call to `Reflector.invokeStaticMethod(...)`, passing the class, method name, and arguments as an array.
- `InstanceMethodExpr`
    - direct: Emit the target, emit the arguments, possibly with casts - also done by `MethodExpr.emitTypedArgs(...)`.  Call the method.
    - reflection: if we know the type (but we didn't have a good match on the method), set up a call to `Reflector.invokeInstanceMethodOfClass(...)`, passing the target, the target class, the method name, and arguments as an array.  If we don't know the type, we call `Reflector.invokeInstanceMethod(...)`.
- `InstanceFieldExpr` 
    - direct: emit the target, get the field value.
    - reflection: set up a call to `Reflector.invokeNoArgInstanceMember(...)`. As mentioned above, this one hides a trick.  It can call a zero-arity method or access a field, depending on what is found.

There is some complexity buried in the various ancillary methods used to do method lookup, best-match selection, argument casting, and runtime dispatch.  Let's dig in.

`Reflector.getMethods`  - used both at compile-time and during runtime reflection.  
 `Compiler.getMatchingParams(...)`

### HostExpr parsing on the CLR

For ClojureCLR, the type hierarchy around host interop expressions looks like:

<img src="{{site.baseurl | prepend: site.url}}/assets/images/hostexpr-type-dependencies.png" alt="Graph of all types related to HostExpr" />

The JVM is simpler;  it has fewer subclasses of `HostExpr`.  The CLR complications arise from the need to handle properties, which do not exist on the JVM.  Unfortunately, this copmplication means that the parsing logic differs enough that we need to treat them separately.  We'll start with the JVM version.


Reflection regarding type members -- methods vs fields vs properties -- differs non-trivially in CLR-land.  ClojureCLR using the Dynamic Language Runtime (DLR) to handle reflection also has a bearing on how to handle ambiguity in the input.   So I chose a somewhat different approach to parsing host expressions.

The first step in parsing is to regularize the syntactic variants into a common form, identifying 

- the target -- either an `instance-expr` or a `Classname-symbol`
- the member symbol -- the name of the field/property/method
- the argument list -- possibly empty
-
If the expression looks like `(. x (method-symbol ...))`,
then this form is required to to be used for a method call, even if there are no arguments.

If the method-name looks like `-field-symbol`, then we are dealing with a field access.  We set a flag and strip off the leading `-` to get the actual member name.

A parsing error will be thrown if the syntax is invalid.

The next step is to determine if we are dealing with an instance member or a static member.  We call `HostExpr.MaybeType` to determine the target type. (I coveered that method in  [Are you my type?]({{site.baseurl}}{% post_url 2025-03-01-are-you-my-type %}).)  If the result is null, we analyze the target expression to get an AST node for it.  (This section is the same as the JVM version.)

For the CLR only, we next look at the first element (if there is one) of the argument list to see if is of the form `(type-args type-arg1 type-arg2 ...)`.  If so, we extract the type arguments and remove that element from the argument list.  This is how we handle generic methods.

At this point, we have:

- `target` - first element after the `.`
- `methodSym` - the member symbol
- `isPropName` - true if we are dealing with a required field/property access
- `staticType` - the type the target resolves to, or null
- `instance` - if `staticType` is null, the AST node for the target expression; else null
- `args` - list of argument expressions (with the type-args removed, if originally present)
- `methodRequired` - true if the syntax required this to be a method call
- `typeArgs` - list of type argument expressions (CLR only) or null

The critical next step is deciding if we are dealing with a zero-arity call.  The test is

```C#
bool isZeroArityCall = RT.Length(args) == 0 && !methodRequired
```

If `methodRquired` is true, then we are definitely dealing with a method call, even if there are no arguments.  This is the difference between the source looking like

```clojure
(. target member)  ;; could be field/property access or zero-arity method call
```

and 

```clojure
(. target (member)) ;; definitely a method call
```

Separate code paths handle zero-arity calls and non-zero-arity calls.

Zero-arity calls have several possibilities:

| Target | Other condition | Member found? | Node type |
|--------|---------------|-------------|
| Static | No type args | Static field  | `StaticFieldExpr`|
| Static | No type args | Static property | `StaticPropertyExpr` |
| Static | Not a property | Static zero-arity method | `StaticMethodExpr` |
| Static |                | None found | Error |
| Instance, known type | No type args | Instance field | `InstanceFieldExpr` |
| Instance, known type | No type args | Instance property | `InstancePropertyExpr` |
| Instance, known type |              | Instance zero-arity method | `InstanceMethodExpr` |
| Instance, known type | Assign context |  | `InstanceFieldExpr` |
| Instance, known type | Not assign context |  | `InstanceZeroArityCallExpr` |
| Instance, unknown type | Assign context |  | `InstanceFieldExpr` |
| Instance, unknown type | Not assign context |  | `InstanceZeroArityCallExpr` |


You will note that having type arguments means this must be a method call; the CLR does not allow generic fields or properties.

If we are in the static call situation and we can't find a matching member, then we have an error.  Similarly, if we are in the instance call situation with a known type and can't find a matching member, we do allow for the opportunity to be passed some object that has a matching member at runtime;  this is a reflection situation.  Obviously, if we do not the type, we are in a reflection situation.
If we are in an assignment context, meaning that this expression is the target of a `set!`, then we must be dealing with a field access;  otherwise, we create an `InstanceZeroArityCallExpr` node.
Both will deal with reflection at code generation time.  Notable, a `InstanceZeroArityCallExpr` always indicates reflection; in addition, it has to figure out at runtime if it is dealing with a method call, or a property or field access.

If we are not in zero-arity call situation, or we are `methodRequired`, we parse any arguments and create either a `StaticMethodExpr` or an `InstanceMethodExpr` node.  At code generation, we will do lookup of the method to be called and decide if reflection code is needed.


