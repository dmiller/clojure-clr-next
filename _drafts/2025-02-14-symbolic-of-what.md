---
layout: post
title: Symbolic of what?
date: 2023-01-06 00:00:00 -0500
categories: general
---

An exercise in symbology.


## Introduction 

I remember trying to figure out symbols when I first learned Lisp.  
My predecessor languages (Fortran, Basic, Pascal) had not prepared me.  
(You might guess from that list that my first encounter was some years ago.)  
I was in good shape with symbols across multiple dialects of Lisp over the years, 
though certainly there was non-trivial variation. 

Clojure forced yet another re-calibration.  
Symbols are a simple construct, but are given meaning by a complex web of interactions
among the Lisp reader, namespaces, the Clojure compiler and the Clojure runtime.
I hope to document here where meaning arises.



## Background

One should go the documentation.

- [Learn Clojure -Syntax: Symbols and idents](https://www.clojure.org/guides/learn/syntax#_symbols_and_idents)
- [ The Reader: Reader forms: Symbols](https://clojure.org/reference/reader#_symbols)
- [Data Structures - Symbols](https://clojure.org/reference/data_structures#Symbols)


Apparently that is not enough for some.

- [The Relationship Between Clojure Functions, Symbols, Vars, and Namespaces](https://8thlight.com/insights/the-relationship-between-clojure-functions-symbols-vars-and-namespaces) by Aaron Lahey.  (Kudos for the Oxford comma.)
- [What are symbols in Clojure?](https://www.reddit.com/r/Clojure/comments/j3b5hc/what_are_symbols_in_clojure/?rdt=63497)
- [Explain Clojure Symbols](https://stackoverflow.com/questions/1175920/explain-clojure-symbols)

The first article is especially releveant. 

So much for preparation.

# Naked symbolism

The `Symbol` data structure is relatively simple -- it has an optional namespace and a name, both strings.





