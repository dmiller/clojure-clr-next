---
layout: post
title: C4 - Compiler structure
date: 2025-03-11 00:00:00 -0500
categories: general
---

A quick look at the structure of the compiler.

##  Compiler structure

The phases of a compiler are often expressed in these terms:

- Lexical analysis: take the stream of characters of the source code and output a stream of 'tokens'.
- Syntax analysis: Take the stream of tokens and determine its hierarchical structure, outputing an abstract syntax tree (AST).
- Semantic analysis: check the semantic consistency of the AST, including making sure that referenced elements exist, types are correct, etc.
- Intermediate code generator: take the typed AST and generate code for an abstract machine
- Code optimizer: optimizze the intermediate code
- Code generator: generate machine code

(Search for "compiler phases" using your favorite search engine.)

The compiler for Clojure has a modified structure because (1) it is a Lisp and (2) it runs on an execution engine (JVM, CLR, ...).   The phases of the Clojure compiler are:

1. Read
2. Analyze
3. Generate

For Lisps generally, the Lisp reader combines lexical analysis (tokenization) with the first stage of syntactic analysis: the determination of hierarchical structure.  Tokenization breaks an input string such as

```clojure
(def x [72 "abc"])
```

into individual tokens --  `(`, `def`, `x`, `[`, etc. -- but structures the output into Lisp data structures:


list of:
  - `def`
  - `x`
  - vector of: 
      - `73`
      - `"abc"`
  

This yields an analysis of the input text as tree of tokens; we still must analyze this tree interpreting it as code rather than just data structure.

After the reader has done its work, the resulting Lisp data structure is passed to the compiler.
The compiler essentially performs two passes:

1. Generate an abstract syntax tree from the data structure; and
2. Traverse the AST to evaluate it or generate code.

The first step is a combination of the second half of traditional syntatic analysis phase -- generating the AST -- with the traditional semantic analysis phase.   The second step generates to an intermediate language for an abstract machine -- that is excactly what Java bytecodes or MSIL instructions are.  The optimization and machine code generation are handled by the execution engine.             


This description is a simplification.  For example, there is code generation for some constructs during the first phase.  (One of my goals is to figure out if this is really necessary.)  But we'll get to that later.
