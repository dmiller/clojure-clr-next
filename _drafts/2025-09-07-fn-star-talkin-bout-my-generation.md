---
layout: post
title: C4 - fn*: talkin' 'bout my generation
date: 2025-09-07 00:00:00 -0500
categories: general
---

We look at code generation for functions in ClojureCLR.

In a previous post ([C4: Functional anatomy]({{site.baseurl}}{% post_url 2025-09-04-functional-anatomy %})), we looked at how functions are represented in ClojureCLR.  That post focused on the interfaces and classes that form the basis of the representation of functions.  When we generate a function in ClojureCLR, though we are deriving a class from one of the base classes (typically `AFunction` or `RestFn`), there is a significant amount of support structure that gets added.  That is the topic of this post.

## Our playground

The primary classes involved in function code generation are:

<img src="{{site.baseurl | prepend: site.url}}/assets/images/objexpr.png" alt="Graph of all types related to ObjExpr" />

I have no idea why `ObjExpr` and `ObjMethod` are named what they are.
`FnExpr` is the AST node that represents an `fn*` form;  `FnMethod` represents an `invoke` method of the generated class. 
`NewInstanceExpr` represents a `deftype` or `reify` form; `NewInstanceMethod` represents a method of the generated class.
For these, a significant amount of code lies in the base classes `ObjExpr` and `ObjMethod`. We will focus here on `FnExpr` and `FnMethod`.  Most of this analysis applies to `NewInstanceExpr` and `NewInstanceMethod` as well.

Note: Do not confuse `NewInstanceExpr` with `NewExpr` -- the latter represents a `new` form that creates an instance of a class.

## Let me count the lines

| File | Source lines | Executable lines |
|------|--------------:|------------------:|
| ObjExpr.cs |  1,195 | 450 |
| FnExpr.cs  |  362 |  102 |
| NewInstanceExpr.cs |  681 | 227 |
| ObjMethod.cs |  176 |  50 | 
| FnMethod.cs  |  458 |  157 | 
| NewInstanceMethod.cs |  321 |  117 |

Doesn't seem like much?  It's packed.  And dependent on a some other goodies that we will get to in due course.

## Out of control

If you try to track the flow of data in the parsing and code generation, particularly with respect to functions, you will soon find yourself in a pit of despair.    I am going to ignore that aspect of the code for now.
You can get the details in the upcoming [C4: Out of control][TBD] post.

What is important for now is to know that, by whatever obscure means, the parsing of forms in the methods of an `fn*` collects information that eventually is folded into either the `FnMethod` or the `FnExpr` instance that is being generated.


