---
layout: post
title: Say my name. ... Heisenbug.
date: 2024-12-10 00:00:00 -0500
categories: general
---

Oft cited, rarely sighted, now sited ... here.

## Now you see it ...

I recently upgraded ClojureCLR to run on .NET 9 (in addition to .NET 6, .NET 8, and .NET Framework 4.x).  The work required should have been as simple as going into `Clojure.csproj` and changing

```xml  
<TargetFrameworks>netstandard2.1;net462</TargetFrameworks>
```

to

```xml  
<TargetFrameworks>net60;net80;net90;net462</TargetFrameworks>
```

(Well, and also adding `net90` in a few other places of no relevance to this story.)

I did this.  And it failed.  Forget about running tests.  At startup, the Clojure source code that defines `clojure.core` and other core environment features was blowing up.  "Bad type".

I know what that means. ClojureCLR was looking up a type and couldn't find it.  Unfortunately, the error message does not specify which type it was looking for.  (I've been meaning to fix this for some years. It's going to take some work.  In Clojure(JVM), the error thrown by `java.net.URLClassLoader/findClass` is more informative.  In ClojureCLR, it pops up as a NullReferenceException; not helpful.)

So I do what I usually do.  Toss in a breakpoint and run it in the debugger.

## ... and now you don't.

The bug disappeared.

This is the official definition of a [_Heisenbug_](https://en.wikipedia.org/wiki/Heisenbug): "a software bug that seems to disappear or alter its behavior when one attempts to study it."

It didn't "seem" to disappear. It _disappeared_.

Where the breakpoint was set made a difference.  If the breakpoint was encountered before the error, the error would not occur.  If the breakpoint was set to be hit after the point of error, the error would occur -- a bit too late for me to get useful information.

We will not discuss how many debugging print statements I inserted or how many places I set breakpoints.  But eventually I discovered that the determining factor was the point in time when `netstandard.dll` was loaded.  I had gotten desperate enough to stare at the debug output window  looking at DLL loading messages.  If `netstandard.dll` was loaded before the error, the error would not occur.  If it was loaded after the error, the error would occur.

Wait a minute, I hear you say, we got rid of `netstandard`.  Yes.  Indeed.  The debugger was loading `netstandard.dll` into the ClojureCLR running process. And it was supplying the type we were looking for and making the error go away.

## The real explanation

The error was occurring due to an `:import` clause in a `ns` form. Picking one of several examples:

```clojure
(ns clojure.core.server 
  (:require ... )
  (:import ... 
    (System.Net Dns)))
```

If you check the docs, under Framework 4.x, `System.Net.Dns` is defined in `System.dll`.  Under Framework 4.x, the import works fine.  Running under framework .NET Standard 2.1, the import works fine -- `System.dll` has to be there to get things started.

 Under .NET 6 and later, `System.Net.Dns` is defined in `System.Net.NameResolution.dll` -- and that DLL had not been loaded at the time of the import.

By some magic, when the debugger injects `netstandard.dll` into the process, it picks up class `System.Net.Dns`.  I don't understand fully how this works. That DLL is full `TypeForwardedTo` directives and references to a ton of DLLS -- including our friend `System.Net.NameResolution.dll`.


## The fix

THe fix is to move the import out of the `ns` form.  After the `ns` form, we load the appropriate DLL, then do the `import` by direct call.

Because this is a framework DLL, the regular assembly loading call won't work.  We have to specify pulling from the the system runtime directory, thus:

```clojure
(try 
  (assembly-load-from (str clojure.lang.RT/SystemRuntimeDirectory "System.Net.NameResolution.dll"))
  (catch Exception e))  

(import '[System.Net Dns])
```

The `try` wrapper is necessary here because this code is also used by ClojureCLR running on .NET Framework 4.x.  We expect the load to fail in the environment.  

The Clojure mechanisms for class loading were set up to work nicely with Java classpaths, jar files, and the like.  The coarser granularity of assemblies in .NET, string notions of type identities, and the like, do complicate our life a bit in ClojureCLR.  

One could put the `assembly-load-from` call ahead of the `ns` form, but that messes up the libraries for source code analysis that expect the `ns` form to be the first thing in the file.

I have occasionally contemplated adding some type of `:assembly` directive to the `ns` form, but I know the sentiment among the Clojure designers is that `ns` is already too complicated.

But, for now, I'll just embrace this kludge.
