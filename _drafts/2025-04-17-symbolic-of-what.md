---
layout: post
title: C4 - Symbolic of what?
date: 2023-04-17 00:00:00 -0500
categories: general
---

We look at the interpretation of symbols in Clojure code.


## Introduction 
 
Symbols but are given meaning by a complex web of interactions among the Lisp reader, 
namespaces, the Clojure compiler, and the Clojure runtime.

We'll skip the reader, though the interpretation of symbols as discussed below does come into just a bit in the reading of syntax-quote (` `` `) forms.  But that's a bit off the path we need to travel.

The code for resolving symbols and translating them in context into nodes in the abstract syntax tree (AST) is complex.
There are appear to be some reduncancies that could be eliminated, along with a few other simplifications.
But for that, I needed more clarity on the rules for symbol interpretation.  What follows is not complete, by any means, but it is a starting point.

## An example

Let's warm up with a simple example.  Suppose we have already loaded in the following code:


```Clojure
(ns namespace.with.a.long.name)

(defn g [z] (inc z))
(defn h [x] (g x))

(ns ns1 
  (:require [namespace.with.a.long.name :as ns2]))   

(defn f [x y z] [z y x])
```

Now consider analyzing the following code with `ns1` as the current namespace.

```Clojure
(fn* [x] 
   (let* [y  7]
      (f (ns2/g Int64/MaxValue y) 
         (String/.ToUpper x)
         (namespace.with.a.long.name/h System.Text.StringBuilder))))
```
(Note: the parser would see `fn*` instead of `fn` -- the latter is macro that expands to the former. Similarly for `let*`.)
We will focus on the call to `f`.  The analysis at that point will be in a context were two local binding scopes have been set up, for local variables `x` and `y`.


Within the call to `f`, we must interpret each symbol that occurs in the form, including `f` itself.


```Clojure
f  x  y  ns2/g  namespace.with.a.long.name/h  System.Int64  String/ToUpper System.Text.StringBuilder
```

`x` and `y` are easy.  They do not have a namespace, so they could be local bindings.  Local binding takes precedence over other possible interpretations.  Indeed, the current context has local binding for those symbols.  The analyzer will produce `LocalBindingExpr` nodes for them.

`f` also does not have namespace.  However, it not bound in the current lexical scope.  
It does not have a namespace, so it does not refer to directly or indirectly (via an alias) to a namespace.
The remaining option is that it has a mapping in the current namespace.  It does, to a `Var` and that is what we use. The analyer will produce a `VarExpr` node for it.

`ns2/g` is a bit more complicated.  It has a namespace, so it can't be a local binding.  We need to determine what namespace `ns2` stands for.  This requires looking up `ns2` in the current namespace.  The current namespace is `ns1`, which has an alias for `namespace.with.a.long.name`.  So we look up `g` in `namespace.with.a.long.name`, finding a `Var`.  We also check to see if `g` is private.  It is not, so we can use it.  The analyzer will also produce a `VarExpr` node.

`namespace.with.a.long.name/h` is also easy.  `namespace.with.a.long.name` is not an alias but the name of an existing namespace.  And `h` is a public `Var` in that namespace.  So we can use it.  The analyzer will produce a `VarExpr` node for it.

Next consider `Int64/MaxValue`. It does have a namespace, so it can't be a local.  We check for if `Int64` is a namespace alias; it is not.
However, the `ns1` namespace does have a mapping from the symbol `Int64` to the type `System.Int64`.  (By default, all namespaces are set up with mappings to 'system' types from their unqualified names.)   So we have a symbol with the namespace mapping to a type.  We must check to see if the name of the symbol, in this case `MaxValue` is a property or field in that type.  There is such a property in the type `System.Int64`, so we can use it.  The analyzer will produce a `StaticFieldExpr` node for it.

`String/.ToUpper` is similar.  In this case, because this symbol appears in the functional position of function invocation, 
given that `String` maps to `System.String`, we look for methods also. Beacause the name starts with a period, we look for an instance method, and find one.  In this case, there will not be a node separately for `String/.ToUpper`; rather, the analyzer will create an `InstanceMethodExpr` node for the entire expression.  

Finally, we have `System.Text.StringBuilder`.  When we have a symbol with no namespace and periods in the name, we look for a type.
In this case, we do find a type.  If it didn't name a type, we would go on and treat the same as a symbol with no periods. (And probably fail).  To express the type in the AST, the analyzer will create a `ConstantExpr` node.  


## A look at the code

We can profitably take a look at the actual C# code for `Compiler.AnalyzeSymbol`.  

```C#
private static Expr AnalyzeSymbol(Symbol symbol)
{
    // The tag on the symbol will be used to pass along user-specified type hints to various constructs that will use them.
    Symbol tag = TagOf(symbol);

    // Local bindings take precedence.  Only a symbol without a namespace can be locally bound.
    if (symbol.Namespace == null) // ns-qualified syms are always Vars
    {
        //  See if there is a local binding for the symbol.
        //  If there is a local binding for the symbol, we will use it.
        //  In such a case, there is a side-effect hidden in ReferenceLocal.  See below.
        LocalBinding b = ReferenceLocal(symbol);
        if (b != null)
            return new LocalBindingExpr(b, tag);
    }
    else
    {
        // The symbol has a namespace.  
        // We must make sure the namespace of the symbol does not refer to an actual namespace;
        //    being a namespace name or alias overrides type names.
        // The IsPosDigitCheck is to defer things like String/2 to a later stage.

        if (namespaceFor(symbol) == null && !Util.IsPosDigit(symbol.Name))
        {
            // Check the namespace to see if it names a type.  (More on HostExpr.MaybeType below.)
            Symbol nsSym = Symbol.intern(symbol.Namespace);
            Type t = HostExpr.MaybeType(nsSym, false);
            if (t != null)
            {
                // THe namespace of the symbol names a type.   Think of Int64/MaxValue.
                // We look for a field or property in that type with the name of the symbol.
                // If we find one, we will create a StaticFieldExpr or StaticPropertyExpr node.
                // If we don't find one, we will create a QualifiedMethodExpr node -- more on that later.
                // Note that this section of code definitely returns.  
                // If the symbol is Type/Something, one of these three is created.

                FieldInfo finfo;
                PropertyInfo pinfo;

                if ((finfo = Reflector.GetField(t, symbol.Name, true)) != null)
                    return new StaticFieldExpr((string)SourceVar.deref(), (IPersistentMap)Compiler.SourceSpanVar.deref(), tag, t, symbol.Name, finfo);
                else if ((pinfo = Reflector.GetProperty(t, symbol.Name, true)) != null)
                    return new StaticPropertyExpr((string)SourceVar.deref(), (IPersistentMap)Compiler.SourceSpanVar.deref(), tag, t, symbol.Name, pinfo);
                else return new QualifiedMethodExpr(t, symbol);
            }
        }
    }

    // We've ruled out our symbol being a local binding or Type/Something.
    // We need to figure out what it might be.  
    // More on Compiler.Resolve below.
    object o = Compiler.Resolve(symbol);

    Symbol oAsSymbol;

    if (o is Var oAsVar)
    {
        // We resolved to a Var, so the value of the symbol will be the value of the Var.
        // Except for macros.
        if (IsMacro(oAsVar) != null)
            throw new InvalidOperationException("Can't take the value of a macro: " + oAsVar);
        // If the Var is ^:const, we subsitute its value.  
        if (RT.booleanCast(RT.get(oAsVar.meta(), RT.ConstKey)))
            return Analyze(new ParserContext(RHC.Expression), RT.list(QuoteSym, oAsVar.get()));

        // It's just regular Var.  Do a little bookkeeping and return a VarExpr node.
        RegisterVar(oAsVar);
        return new VarExpr(oAsVar, tag);
    }
    else if (o is Type)

        // The symbol resolved to a type.  Make a ConstantExpr node for it.
        return new ConstantExpr(o);

    else if ((oAsSymbol = o as Symbol) != null)

        // A symbol that does not resolve to a Var or a Type, is called an unresolved var.
        // The only way Compiler.Resolve can return a symbol is if *allow-unresolved--vars* is bound to true.
        return new UnresolvedVarExpr(oAsSymbol);

    // The only way to get here is if there is mapping for the symbol in the current namespace that is not a Var or a Type.
    // Life sucks.
    throw new InvalidOperationException(string.Format("Unable to resolve symbol: {0} in this context", symbol));
}
```
## Resolving symbols

A lot of heavy lifting is taking place in that call to `Compiler.Resolve`.




ReferenceLocal
HostExpr.MaybeType
RegisterVar


## There's more

In my work on ClojureCLR.Next, I implemented the parser as a standalone project.
The test suite for the parser has almost 60 tests for symbol interpretation. This includes tests for looking up / resolving symbols in the context of namespaces and aliases and types, plus tests for the various AST nodes that can be created from symbols.

Here is a sample of test descriptions.  
The first two lists are for symbol lookup and resolution, used in parsing, 
but not looking at what AST node would be created.
Do you know all of these rules?

These are when the symbol has namespace:

- ns/name, ns is namespace alias, no var found for name (throws)
- ns/name, ns is namespace alias, not current namespace, var found, var is private, privates not allowed (throws)
- ns/name, ns is namespace alias, not current namespace, var found, var is private, privates allowed (var returned)
- ns/name, ns is namespace alias, not current namespace, var found, var is public (var returned)
- ns/digit, but ns is not a type (throws)  -- this is something like `BadType/7`
- ns/digit, ns is a type (return array type)  -- this is something like `String/1`

These are when the symbol does not have a namespace:

- name has . in it, names type (return type) 
- name has . in it, does not resolve to a type (throws)  -- note that in the parser, we catch the exception and move on
- `in-ns` -- treated as a special case -- always found
- `ns` -- treated as a special case -- always found
- name found in current namespace (return var)  (there are variants in the resolve/lookup code that will create the `Var` if not found)

Several kinds of AST nodes can be created from symbols.  The details of node types is bit beyond where we can go here,
but perhaps you can get the gist:

- ns/name, ns names a `Type`, that type has a field or property with the given name  => `StaticFieldExpr` or `StaticPropertyExpr`
- ns/name, ns names a `Type`, no field or property found, name does not start with a period  => `QualifiedMethodExpr`, Static 
- ns/.name, ns names a `Type`, no field or property found, name starts with a period  => `QualifiedMethodExpr`, Instance 
- ^NotAType TypeName/FieldName, FieldName not in type TypeName => throws because the tag is not a type
- ^IsAType TypeName/FieldName, FieldName not in type TypeName => `QualifiedMethodExpr`, Static, `IsAType` set as tag.
- ^[...types...] TypeName/FieldName, FieldName not in type TypeName => `QualifiedMethodExpr`, Static, `SignatureHint` set

Without a namespace:

- name - has a local binding => `LocalBindingExpr``
- not local, not a type, resolves to a Var, Var is macro => throws
- not local, not a type, resolves to a Var, Var is has `:const true` metadata  => `ConstantExpr` on the value of the `Var`
- not local, not a type, resolves to a Var, Var is not macro, not const => `VarExpr`
- not local, not a type, does not resolve, allow-unresolved = true => `UnresolvedVarExpr`
- not local, not a type, does not resolve, allow-unresolved = false => throws



