---
layout: post
title: C4 - Symbolic of what?
date: 2025-09-02 00:00:00 -0500
categories: general
---

We look at the interpretation of symbols in Clojure code.


## Introduction 
 
Symbols are given meaning by a complex web of interactions among the Lisp reader, 
namespaces, the Clojure compiler, and the Clojure runtime.

We'll skip the reader, though the interpretation of symbols as discussed below does come into just a bit in the reading of syntax-quote forms.  But that's a bit off the path we need to travel.

The code for resolving symbols and translating them  into nodes in the abstract syntax tree (AST) is complex. In face, there appear to be some reduncancies that could be eliminated, along with a few other simplifications.  But let us proceed with the code we have.

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
f  x  y  ns2/g  namespace.with.a.long.name/h Int64/MaxValue String/.ToUpper System.Text.StringBuilder
```

`x` and `y` are easy.  They do not have a namespace, so they could be local bindings.  Local binding takes precedence over other possible interpretations.  Indeed, the current context has local binding for those symbols.  The analyzer will produce `LocalBindingExpr` nodes for them.

`f` also does not have namespace.  However, it not bound in the current lexical scope.  
It does not have a namespace, so we don't need to figure out what its namespace actually is.
The remaining option is that it has a mapping in the current namespace.  It does, to a `Var` and that is what we use. The analyer will produce a `VarExpr` node for it.

`ns2/g` is a bit more complicated.  It has a namespace, so it can't be a local binding.  We need to determine what namespace `ns2` stands for.  This requires looking up `ns2` in the current namespace.  The current namespace is `ns1`, which has `ns2` as an alias for `namespace.with.a.long.name`.  We look up `g` in `namespace.with.a.long.name`, finding a `Var`.  We also check to see if `g` is private.  It is not, so we can use it.  The analyzer will produce a `VarExpr` node.

`namespace.with.a.long.name/h` is also easy.  `namespace.with.a.long.name` is not an alias but the name of an existing namespace.  And `h` is a public `Var` in that namespace.  So we can use it.  The analyzer will produce a `VarExpr` node for it.

Next consider `Int64/MaxValue`. It does have a namespace, so it can't be a local.  We check for if `Int64` is a namespace alias; it is not.
However, the `ns1` namespace does have a mapping from the symbol `Int64` to the type `System.Int64`.  (By default, all namespaces are set up with mappings to 'system' types from their unqualified names.)   So we have a symbol with the namespace mapping to a type.  We must check to see if the name of the symbol, in this case `MaxValue` is a property or field in that type.  `System.Int64.MaxValue` existsThe analyzer will produce a `StaticFieldExpr` node.

`String/.ToUpper` is similar.  In this case, because this symbol appears in the functional position of function invocation and given that `String` maps to `System.String`, we look for methods also. Beacause the name starts with a period, we look for an instance method, and find one.  In this case, there will not be a node separately for `String/.ToUpper`; rather, the analyzer will create an `InstanceMethodExpr` node for the entire expression.  

Finally, we have `System.Text.StringBuilder`.  When we have a symbol with no namespace and periods in the name, we look for a type. In this case, we do find a type.  If it didn't name a type, we would go on and treat the same as a symbol with no periods. (And probably fail).  To express the type in the AST, the analyzer will create a `ConstantExpr` node.  


## A look at the code

Now that we are warmed up, we can profitably  look at the actual C# code for `Compiler.AnalyzeSymbol`.  

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
`Compiler.Resolve` calls `Compiler.ResolveIn` with the current namespace and `allowPrivate` set to `false` (don't allow access to `Var`s marked as private).   I hope the detailed comments in the code suffice to explain what is going on.

```C#
private static object ResolveIn(Namespace n, Symbol symbol, bool allowPrivate)
{
    // Symbol resolution is always relative to some namespace, passed as the first argument to this method.
    // The main discriminator here is whether the symbol has a namespace or not.

    if (symbol.Namespace != null)
    {
        // The symbol has a namespace.  
        // Note that this section of code returns or throws, no fall-through to later code.
        // In the context where this method is called, it is assumed you have already checked
        //   whether the namespace of the symbol names a type.  And that it does not.  
        // Unless it of the form Type/digit.  THat is handled here.

        // First we check to see if the namespace of the symbol names an actual namespace, either an alias or a real namespace.
        Namespace ns = namespaceFor(n, symbol);

        if (ns == null)
        {   
            // It does not name a namespace.  Our only hope is that we have Type/digit.
            Type at = HostExpr.MaybeArrayType(symbol);
            if ( at != null)
                return at;
            throw new InvalidOperationException("No such namespace: " + symbol.Namespace);
        }

        // The namespace of the symbol is a namespace.  Look up the name of the symbol in that namespace.
        // We are only interested in a mapping to a Var.
        Var v = ns.FindInternedVar(Symbol.intern(symbol.Name));
        if (v == null)
            throw new InvalidOperationException("No such var: " + symbol);

        // Note that we might not allow reference to private var in another namespace.
        else if (v.Namespace != CurrentNamespace && !v.isPublic && !allowPrivate)
            throw new InvalidOperationException(string.Format("var: {0} is not public", symbol));
        return v;
    }

    // If we reach here, the symbol does not have a namespace.
    // We first check if the name has . in it.  If so, it had better be a type.
    // This will throw an exception if it does not name a type.
    else if (symbol.Name.IndexOf('.') > 0 || symbol.Name[symbol.Name.Length - 1] == ']')
    {
        return RT.classForNameE(symbol.Name);

    // ns and in-ns are special cases.  They are always found.
    else if (symbol.Equals(NsSym))
        return RT.NSVar;
    else if (symbol.Equals(InNsSym))
        return RT.InNSVar;
    else
    
        // Do not look at this. Do not look at this.  Do not look at this.
        // This relates to some weirdness in the compiler regarding base classes for classes implementing functions.
        // Present in both ClojureCLR and Clojure JVM.
        if (Util.equals(symbol, CompileStubSymVar.get()))
            return CompileStubClassVar.get();

        // We look for the symbol in the namespace that was passed in.
        
        object o = n.GetMapping(symbol);

        // Ignore. Ignore. Ignore.  This relates to double-definitions for types when compiling.  ClojureCLR only.
        if (o is Type type)
        {
            var tName = type.FullName;
            var compiledType = Compiler.FindDuplicateCompiledType(tName);
            if (compiledType is not null && Compiler.IsCompiling)
                return compiledType;
        }

        // If there is no mapping, we can return the symbol itself _only_ if *allow-unresolved-vars* is true.
        // Otherwise, we throw an exception.
        if (o == null)
        {
            if (RT.booleanCast(RT.AllowUnresolvedVarsVar.deref()))
                return symbol;
            else
                throw new InvalidOperationException(string.Format("Unable to resolve symbol: {0} in this context", symbol));
        }

        // There was a mapping.  Return it.
        return o;
    }
}
```

## Dangling references

To finish of this code, some brief comments on a few of the auxiliary methods mentioned above.

`Compiler.ReferenceLocal` is called when we have identified a reference to a local binding.  It does some bookkeeping needed for code-gen.  Specifically, it notes the usage of the local binding in the containing function (if there is one) and any functions above that is might be nested in.  This is so that we know to close over those variables when creating an instance of the function.  It also notes if the local variable is the `this` variable; reference to `this` precludes static linking.  But more about that in [C4: Functional anatomy]({{site.baseurl}}{% post_url 2025-09-04-functional-anatomy %}).

`Compiler.RegisterVar` is similar.  It just notes the reference to the `Var` in the containing function (if there is one).  A field in the class implementing the function will be created and initialized to the `Var` in question.

Looking up types corresponding to names is done in `HostExpr.MaybeType` and `HostExpr.MaybeArrayType`.  I've written about these in [Are you my type?]({{site.baseurl}}{% post_url 2025-03-01-are-you-my-type %}).

## I'm feeling a little testy

In my work on ClojureCLR.Next, I implemented the parser separately from the rest of the compiler, leaving out some of the bookkeeping, deferring type analysis and other semantic meddling for later phases.  This separation allowed me to develop a test suite for just the parser.  This test suite  has almost 60 tests for symbol interpretation alone. This includes tests for looking up / resolving symbols in the context of namespaces and aliases and types, plus tests for the various AST nodes that can be created from symbols.

Do you know all of these rules for intrerpreting symbols?  (I didn't.)  

First, tests for resolving symbols without worrying about AST node construction. These are when the symbol has namespace:

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

Several kinds of AST nodes can be created from symbols.  The details of node types are covered in [C4: AST me anything]({{site.baseurl}}{% post_url 2025-09-01-AST-me-anything %}).    For symbols with a namespace:

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
