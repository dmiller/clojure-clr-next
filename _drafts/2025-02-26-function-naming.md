 ---
layout: post
title: The function of naming; the naming of functions
date: 2025-02-20 00:00:00 -0500
categories: general
---

How the differences between the JVM's classfile model and  .Net's assembly model manifest in ClojureCLR, and thoughts about making some changes.
Specifically, we look at the how function creation interacts with the identity of types across evaluation and compilation.

## Inherent JVM-ness

Clojure was designed to run on the JVM.  Characteristics of the JVM manifest directly in Clojure.  When one tries to implement Clojure on another platform, one becomes painfully aware of assumptions built into the Clojure language.  Nevertheless, I doubt I could list all of them.   In this post I'll concentrate on one aspect: the identity of types.

> When Rich Hickey first started to design and implement what became Clojure, he worked on the JVM and the CLR simultaneously.  He dropped the CLR development to focus on the JVM fairly early.  One wonders how the design of Clojure would differ if he had continued dual development. 
 >   
 >     I also wonder what I would have done with all the time I would have saved from not working on ClojureCLR for the last fifteen years.  But I digress.  
 >       
 >   But I digress.

## JVM: the identity of types

One can spend quite a bit of time working in Java and not give much though about the nature of the identity of types.  You know there are system-defined types floating around, such `java.lang.String`, and there are you own types, maybe `my.funny.Valentine`.  You compile your code, each type ends up in a compiled code file, may `my/funny/Valentine.class`.  There's this _classpath_ thing you need to get right, but otherwise things just mostly work.

Under the surface, the classloader mechanism is used to find and load class files.  Classloaders are hierarchical -- delegation to the parent classloader is a key part of the semantics of type loading.  Any type that is loaded belongs to a classloader.  Thus it is possible to have two types with same fully-qualified name (FQN), such as `my.funny.Valentine` that are distinct because they were loaded by different classloaders.

    There are plenty of explanations of classloaders available online.   You can start with these:
    - [Class Loaders in Java](https://www.baeldung.com/java-classloaders)
    - [JVM Architecture: JVM Class loader and Runtime Data Areas](https://www.javacodegeeks.com/2018/04/jvm-architecture-jvm-class-loader-and-runtime-data-areas.html)
    - [Understanding Extension Class Loading](https://docs.oracle.com/javase/tutorial/ext/basics/load.html)

## Classloaders in Clojure

Beyond just the usual mechanics of getting types loaded into the Clojure at runtime, classloader manipulation is happening under the surface.
Suppose you are sitting at the REPL and you evaluate the following code:

```clojure
(defn f [x] (inc x))    ;; =>  #'user/f
(def f1 f)              ;; =>  #'user/f1, the intent is to capture the underlying function
(defn f [x] (dec x))    ;; =>  #'user/f
(def f2 f)               ;; =>  #'user/f2
```

If you now look at `f1` and `f2`, they are clearly different. 

```clojure
f1                     ;; => #object[user$f 0x7c455e96 "user$f@7c455e96"]
f2                     ;; => #object[user$f 0x1eaf1e62 "user$f@1eaf1e62"]
```
No surprise; they are different objects in memory.  And they appear to have the same class.

```clojure
(class f1)         ;; => user$f
(class f2)         ;; => user$f
```
But, no.

```clojure
(= (class f1) (class f2))    ;; => false
```
And, bingo, they have different classloaders:

```clojure
(.getClassLoader (class f1))    ;; => #object[clojure.lang.DynamicClassLoader 0x30feffc "clojure.lang.DynamicClassLoader@30feffc"]
(.getClassLoader (class f2))    ;; => #object[clojure.lang.DynamicClassLoader 0x985696 "clojure.lang.DynamicClassLoader@985696"]
```

Moreover, we see that the classloaders are of type `clojure.lang.DynamicClassLoader`, defined [here](https://github.com/clojure/clojure/blob/master/src/jvm/clojure/lang/DynamicClassLoader.java).  `DynamicClassLoader` instances are created all the time when running Clojure. The 'E' in 'REPL' is `java.lang.Compiler.eval`:

```java
public static Object eval(Object form) {
	return eval(form, true);
}

public static Object eval(Object form, boolean freshLoader) {
	boolean createdLoader = false;
	if(true)//!LOADER.isBound())
		{
		Var.pushThreadBindings(RT.map(LOADER, RT.makeClassLoader()));
		createdLoader = true;
		}

    ...
```

Every time you evaluate a form at the REPL, a new classloader is created and dynamically bound to a `Var` to be picked up as needed. In `FnExpr`, which is responsible for generating the `user#f` classes seen above, we find:

```java
	synchronized Class getCompiledClass(){
		if(compiledClass == null)
				{
				loader = (DynamicClassLoader) LOADER.deref();
				compiledClass = loader.defineClass(name, bytecode, src);
				}
		return compiledClass;
	}
```

This picks up the dynamically generated classloader and uses it to define the `user#f` class.  Each time, we have a different classloader; each time we get a different class, even if named the same.

## Compilation in Clojure(JVM)

Compilation adds an interesting wrinkle to this story.  Compilation in Clojure takes a namespace and generates (a) a set of classfiles defining the various types generated while loading the file; and (b) a initialization class that has code that mimics the actions of loading the file at the REPL.

The wrinkle is that classfiles themselves do not carry information about classloaders.  There will be only one `user$f.class` generated.  Last one compiled wins.

Consider the following code file, `test/test.clj`:

```clojure
(ns test.test)
(defn f [] (println "I'm first! :) "))
(defn g [] (print "g says: ") (f))
(g)
(defn f [] (println "I'm second? :( but really :)" ))
(g)
```

Now load it:

```clojure
> clj
Clojure 1.12.0
user=> (load "test/test")
g says: I'm first! :)
g says: I'm second? :( but really :)
nil
user=> (test.test/g)
g says: I'm second? :( but really :)
nil
```
The function `g` is set up to access the function `f` by getting the value of the `Var` `#'f`.
In the first call to `g`, `#'f#` is bound to the first definition of `f`. 
In the second call, it is bound to the second definition of `f`.

Start again, this time compiling the file.

```clojure
> clj
Clojure 1.12.0
user=> (compile 'test.test)
g says: I'm first! :)
g says: I'm second? :( but really :)
test.test
user=> (test.test/g)
g says: I'm second? :( but really :)
nil
```

Identical behavior.   Now that we're compiled, start again and do the load.

```clojure
 clj
Clojure 1.12.0
user=> (load "test/test")
g says: I'm second? :( but really :)
g says: I'm second? :( but really :)
nil
```

And now we see the difference.  There is only one `user$f.class` file, and it is the second definition of `f`.  That is the one that is loaded, the only that `g` sees, and that is used in both calls to `g`.  Redefinition does not interact well with compilation. 

It's not that this is unexpected.  [Ahead-of-time Compilation and Class Generation](https://clojure.org/reference/compilation) warns us thus:

> The Clojure compilation model preserves as much as possible the dynamic nature of Clojure, in spite of the code-reloading limitations of Java.

## Direct linking

You get more behavioral differences when you consider [direct linking](https://clojure.org/reference/compilation#directlinking).

> One consequence of direct linking is that var redefinitions will not be seen by code that has been compiled with direct linking (because direct linking avoids dereferencing the var). 

Let's run this one more time, first deleting the class files we created earlier. We then recompile with direct linking turned on.

```clojure
> clj
Clojure 1.12.0
user=> (binding [*compiler-options* {:direct-linking true}] (compile 'test.test))
g says: I'm first! :)
g says: I'm first! :)
test.test
user=> (test.test/g)
g says: I'm first! :)
nil
```

Because `g` does not redirect through `#'f`, it does not see the redefinition of `f`.  
Without direct linking, `g` is coded by

```java
fvar.get().invoke()
```
where `fvar` has been initialized to `#'f`.  With direct linking, the code is

```java
test.test$f.invokeStatic()
```
Here, the class `test.test$f` is the class generated by the first definition of `f`.
When `f` is redefined, `g` does not see the change.  It is hardwired.

Now, go out and load.  

```clojure
> clj
Clojure 1.12.0
user=> (load "test/test")
g says: I'm second? :( but really :)
g says: I'm second? :( but really :)
nil
```

We see the opposite behavior.  Why?  There is only on `test.test$f.class` file to be found on disk.  It is the second one.

> One consequence of direct linking is that var redefinitions will not be seen by code that has been compiled with direct linking (because direct linking avoids dereferencing the var).   

If you compile or load uncompiled with direct linking, `g` will be locked to the first definition of `f`.
If you load compiled code, `g` will be locked to the second definition of `f`.  The behavior changes.  This is one of the meanings of

> The Clojure compilation model preserves as much as possible the dynamic nature of Clojure, in spite of the code-reloading limitations of Java.






