---
layout: post
title: C4 - A time for reflection
date: 2025-09-14 00:00:00 -0500
categories: general
---

Sometimes you can't avoid reflection.  We discuss how Clojure(JVM/CLR) try to avoid reflection and how they handle reflection that can't be avoided.  ClojureCLR differs significantly from ClojureJVM in using the services of the Dynamic Language Runtime (DLR) to handle reflection; we defer that discussion to [Dyn-O-Site][TBD].  This post covers the general details of parsing and reflection handling in ClojureJVM.

## Introduction

Given a host expression, we analyze it to determine if we can resolve the exact type and specific member (method, property, field, constructor) to be invoked.  If we can, then there is no need for reflection:  We can compile the host expression to a direct call to the member, possibly with type casts on arguments and the return value to ensure conformance to the member's signature.  If we cannot determine the exact type+member, we need to generate code that uses reflection to determine the member to be invoked at runtime.

If you need a refresher on host interop expressions, refer to [Java interop](https://clojure.org/reference/java_interop). There are some extensions to the syntax of host interop expressions in ClojureCLR; these will be covered in [Dyn-O-Site][TBD].   


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

The first significant step is to determine if we are dealing with an static member or an instance member.  The first element after the `.` tells the tale:

```Java
Class c = maybeClass(RT.second(form), false);
//at this point c will be non-null if static
Expr instance = null;
if(c == null)
    instance = analyze(context == C.EVAL ? context : C.EXPRESSION, RT.second(form));
```

At this point, if `c` is non-null, we are dealing with a static member;  otherwise, we are dealing with an instance member, and `instance` holds the AST node for the target expression.

Next we try to determine if we are looking at a field access or a method call.  The first requirement is that our form looks like `(. target symbol)`:

```Java
boolean maybeField = RT.length(form) == 3 && (RT.third(form) instanceof Symbol);
```

We then check if there is a zero-arity method of the given name -- unless we have an instance call and the name starts with`-`, in which case we are definitely dealing with a field access.  We still might be a field if there no zero-arity methods.  Note that we can only check for methods in the instance case if the instance expression has a type.

```Java
if(maybeField && !(((Symbol)RT.third(form)).name.charAt(0) == '-'))
    {
    Symbol sym = (Symbol) RT.third(form);
    if(c != null)
        // We might be a field if there are no zero-arity methods (static case)
        maybeField = Reflector.getMethods(c, 0, munge(sym.name), true).size() == 0;
    else if(instance != null && instance.hasJavaClass() && instance.getJavaClass() != null)
        // Same, instance case
        maybeField = Reflector.getMethods(instance.getJavaClass(), 0, munge(sym.name), false).size() == 0;
    }
```

What happens next depends on whether `maybeField` is true or not.

If `maybeField` is true, we _might_ be looking at a field access. 
We will create either a `StaticFieldExpr` or an `InstanceFieldExpr` node.
For `InstanceFieldExpr`, we pass a flag indicating if the name started with a `-`; if it does then we are definitely dealing with a field access.  Don't be fooled:  An `InstanceFieldExpr` can generate reflection code that can access either a method or field at runtime, depending on what the runtime type of the `instance` expression turns out to be.  See below.

If `maybeField` is false, we are looking at a method call.  We create either a `StaticMethodExpr` or an `InstanceMethodExpr` node, depending on whether we are dealing with an instance or static member.
The constructors for each of these node types does some additional analysis.

- `StaticMethodExpr`:
    - we have a known static type, we have a name, we have an arity.  If there are no methods of the given name and arity on the given type, we throw an error.  
    - if there is more than one method of the given name and arity, we try to resolve to the best match based on the method argument types and the types of the provided arguments. 
    - if we cannot pick a best match, we will generate a reflection call during code-gen.  (Maybe) print a warning.
    - if we have a best match, we will be able to generate a direct call during code-gen.
    - if we have a direct match and `*unchecked-math*` is `:warn-on-boxed` and the method is on the list of 'boxed match' methods, (maybe) print a warning.

- `InstanceMethodExpr`:
    - if we do not know the type of our target, we will be generating reflection code during code-gen. (Maybe) print a warning.
    - if we do know the type, the process is similar to `StaticMethodExpr` -- look for methods of the given name and arity, try to find a best match, etc.  No boxing warnings, though; those methods are static only.  There is an additional step to if we find a method but its declaring class is not public: we look up the class hierarchy to see if there is a public superclass that also declares the method. 

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

## JVM Reflection details

A major piece of the puzzle is determining if a method exists matching a particular name and arity, and if so, picking the best match based on the types of the provided arguments.  This is handled by several methods in `Reflector` and `Compiler`.  

`Reflector.getMethods`  is used at compile-time and during runtime reflection. At compile-time, it is usually followed by a call to `Compiler.getMatchingParams(...)` to pick the best match.  At runtime, determining the best match is usually determined as part of a call to `Reflector.invokeMatchingMethod(...)`.  

`Reflector.getMthods(Class c, int arity, String name, boolean getStatics)`  is straightforward.
Find all the methods on class `c` and filter for name, arity, and static or not.  There is some code to handle bridge methods, which result from generic type erasure; they are considered only if no non-bridge methods match.  Also, if `c` is and interface and we are not looking for statics, we also look at the class `Object` for matching methods.  That's it.

`Compiler.getMatchingParams()`  is more complex.  Its signature is:

```Java
static int getMatchingParams(
    String methodName, 
    ArrayList<Class[]> paramlists, 
    IPersistentVector argexprs,
    List<Class> rets)
```

- `methodName` - name of the method we are trying to match.  Used only for a possible error message.
- `paramlists` - list of parameter type arrays, one per candidate method.
- `argexprs` - list of argument expressions.
- `rets` - list of return types of the candidate methods, in the same order as `paramlists`.  This is an output parameter.

The possible results are:

- exact match - there is one method whose parameter types exactly match the argument expression types; the index of this method is returned.
- best match - there is no exact match, but there is one method that is the better than the others; the index of this method is returned
- (explained below)
- tie - there is more than one method that is equally good;  an error is thrown.
- no match - no method matches;  -1 is returned.

`getMatchingParams` is a rather condensed 50 lines of code.  Unwrapping the logic, roughlyit does the following for each method:

- check the parameter types against the argument expression types.  We count the number of exact matches.  If a parameter is not an exact match, we call `Reflector.paramArgTypeMatch(...)` to see if the argument type can be converted to the parameter type. (See below.)  If not, this method is not a match; move on to the next method.
- if all parameters are exact matches, we have an exact match; record its index and note that we have an exact match.  The code here is a little obscure;  there is some possibility of more than one exact match on parameters -- I'm not sure how that happens on the JVM -- so there is some code to decide if we prefer this one over the previous exact match, based on return type. But we come out of this with the index of some exact match method and the 'exact match' flag set to true.
- if we do not have an exact match, but all parameter types are compatible with all expression types, and we haven't already recorded a match, we record the index of this method as a possible best match.  If we have already recorded a possible best match, we need to determine if we prefer this method to the previously recorded best match.  The outcomes here are:  prefer previous method; prefer current method; or tie.

At the end of this, if we have a tie recorded, we throw an error.  Otherwise, we return the index that has been recorded, or -1 if none matched.

Two loose ends:  `Reflector.paramArgTypeMatch(...)` and method preference logic.

```Java
static public boolean paramArgTypeMatch(Class paramType, Class argType)
```
 is a bunch of cases.  
 
 - If `argType` is null, meaning we have no clue, we match unless `paramType` is a primitive. 
 - If `paramType` is identical to `argType`, it's a match.
 - If `paramType` is assignable from `argType`, we match.  
 - If `paramType` is a an FI method and `IFn` is assignable from `argType`, we match.
 - Then we have a bunch of cases for when the `paramType` is a primitive type.  We check for `argType` to be either primitive or primitive wrapper types that can be converted to `paramType`.  Both widening and narrowing conversions are allowed here; a `long` parameter can match an `int` argument, and vice versa.
 
 The method preference logic is based on a subsumption test:

```Java
static public boolean subsumes(Class[] c1, Class[] c2){
	//presumes matching lengths
	Boolean better = false;
	for(int i = 0; i < c1.length; i++)
		{
		if(c1[i] != c2[i])
			{
			if(!c1[i].isPrimitive() && c2[i].isPrimitive()
			   ||
			   c2[i].isAssignableFrom(c1[i]))
				better = true;
			else
				return false;
			}
		}
	return better;
}
```



----------------------------------------------------------------------------------------
- used both at compile-time and during runtime reflection.  
 `Compiler.getMatchingParams(...)`  uses `Reflector.paramArgTypeMatch`, `Compiler.subsumes`



 `Reflector.getAsMethodOfPublicBase(...)` deprecated.  getMethods, isMatch
 `Reflector.getAsMethodsOfAccessibleBase()`  deprecated.  getMethods, isAccessbleMatch

 `MethodExpr.emitTypedArgs(...)`
 `Reflector.invokeStaticMethod(...)`  getMethods + invokeMatchingMethod
 `Reflector.invokeInstanceMethodOfClass(...)`  getMethods + invokeMatchingMethod
 `Reflector.invokeInstanceMethod(...)`
 `Reflector.invokeNoArgInstanceMember(...)`  - getmethods + invokeMatchingMethod or getInstanceField










