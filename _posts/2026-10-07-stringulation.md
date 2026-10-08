---
layout: post
title: Stringulation
date: 2026-10-07 00:00:00 -0500
categories: general
---

I've been stringing you along for years.  Time for a (breaking?) change?

## Shall I compare thee to a ... string?

When porting Clojure for the JVM to the CLR, the question often arises of when a particular aspect of computation is intrinsic to Clojure or just an exposure of a feature of the JVM.  For the former, I try to duplicate behavior; for the latter, I try to expose the corresponding CLR behavior.  I have run into this not infrequently, particularly in the early days of the porting effort.

One area where it came up very early was with string comparison.  And, frankly, I did not give it a lot of thought.  Where ClojureJVM used `java.lang.String.compareTo`, I used `System.String.CompareTo`.   In other words, I made the decision to expose the underlying platform mechanism.   In retrospect, this was probably not the right decision. These methods are significantly different.  

The JVM `String.compareTo` is an ordinal comparison of UTF-16 strings.  This is a straightforward lexicographic comparison, performed character-by-character.  The CLR `String.CompareTo`  is culture-sensitive, i.e., the result of comparing two strings varies depending on the culture in effect at that time, which is thread-dependent.  

There are several consequences of using `CompareTo` on the CLR, of varying import.

**ClojureJVM and ClojureCLR differ on things such as comparisons.**

```clojure
(compare "a\u00ADb" "ab") ;; => non-zero meaning not equal (JVM)
(compare "a\u00ADb" "ab") ;; => zero, meaning equal        (CLR) 
```

(`\u00AD` is the soft-hyphen character.)



**And, thus, sort order:**

```clojure
(sort ["b" "B" "a" "A"]) ;; => ("A" "B" "a" "b")  (JVM)
(sort ["b" "B" "a" "A"]) ;; => ("a" "A" "b" "B")  (CLR)
```

**`compare` and `=` don't agree.**

```clojure
(compare "a\u00ADb" "ab") ;; => 0  (equal)     (CLR)
(= "a\u00ADb" "ab")       ;; false (not equal) (CLR)
```

The difference here is that `compare` uses `String.CompareTo` (culture-sensitive) and `=` uses  `String.Equals` (ordinal comparison).

**`sorted-set` and `hash-set` yield different sets on the same inputs.** 

```clojure
 (count (sorted-set "a\u00ADb" "ab")) ;; => 1, one string silently dropped because it compares as equal (CLR)
 (count (hash-set "a\u00ADb" "ab"))   ;; => 2, because hash sets use ordinal = (CLR)
```

**Culture-sensitivity bites in some related places where string comparisons occur.**

For example,

```clojure
(compare :b :B) ;; => 32 (positive, indicating :b > :B) (JVM)
(compare :b :B) ;; => -1 (negative, indicating :b < :B) (CLR)
```

**Culture-sensitive string compares are thread-dependent.**

For example, ASP.NET Core sets the culture per request from `Accept-Language`, so `(sort names)` silently follows each user's collation.  Picking up information from the thread is fine; I feel it is better for it to be a deliberate choice rather than delivered behind your back.

**Culture-sensitive comparisons are more expensive than ordinal comparisons.** 

Sorting a bunch of strings or creating a sorted map under ordinal comparison executes roughly 54-61% fewer instructions (instruction counts measured on one machine, not timings) than a culture-sensitive comparison using `en-US`.   Capturing one culture comparer at startup to avoid thread lookup of the culture decreases the instruction count by roughly 5% compared to `String.CompareTo` today.    

**Culture-sensitive comparisons have not been consistent over time.**

"Before .NET 5, the .NET globalization APIs used different underlying libraries on different platforms. ... If you upgrade your app to target .NET 5 or later, you might see changes in your app even if you don't realize you're using globalization facilities." (See [Globalization and ICU - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu)).  And you can switch globalization providers.   ClojureCLR on .NET Framework uses NLS, not ICU, so Framework and .NET builds of ClojureCLR on the same machine can yield different results.  That's just on Windows.  Let us not discuss Linux and Mac.  Read it and weep.

## Going deeper

The original focus in the benchmarking investigation surfaced the `compare` issue outlined above.  After the initial results, I decided to expand the search to all string manipulation in the ClojureCLR, with comparisons against ClojureJVM where appropriate.  Most of this internal string manipulation is related to reading data (which in Lisp-land includes programs) which arguably should not be culture/locale sensitive. It is not in ClojureJVM -- all is ordinal.  There are a few places where culture-sensitivity snuck into the ClojureCLR code, by carelessness or ignorance.  Some are so marginal that I'm guessing they have never been encountered.  For example, if we let `<SHY>` represent the soft-hyphen character, when reading source code:

- JVM reads `foo:<SHY>` => creates symbol `foo:<SHY>`
- CLR reads  `foo:<SHY>` => throws an exception under `en-US` (at least)

The most significant culture-dependent bugs are

- `tr-TR`: the flag to turn on direct linking in the compiler is ignored -- the dotless 'i' (**U+0131**) makes an appearance.
- `sv-SE`: `(+ 1 1E-10M)` doesn't compile.  Swedish uses a different minus sign character.

I consider these examples to be bugs.  Fixing them is not a breaking change.

There are five functions (`starts-with?`, `ends-with?`, `index-of`, `last-index-of`, `replace-first`) in the  `clojure.string` library that are in conflict with the JVM version and also demonstrably incorrect.  These bugs are visible to users today, but they are bugs and should be fixed.  Example:   `(replace-first "a<SHY>b" "ab" "X")` gives `"Xb"` and corrupts the string.  Results would change only for strings containing ignorable characters (soft hyphen, `NUL`, combining marks) and the changes would match the JVM's answers.

## A proposal

I plan to make two sets of changes.  The first set is to fix the bugs above: the reader, the compiler, number literals, and `clojure.string`.  I am not `<SHY>` about making these changes.

The second set of changes will be more user-visible and thus have the potential to break user code.    This involves changing the definition of `compare` to be ordinal-based.   To minimize impact, we can make this change _selectable at startup_.  Set the switch to 'culture'  and you get the current behavior;  set it to 'ordinal' and you get ordinal-based comparisons.  The 'ordinal' mode is pretty much identical to ClojureJVM behavior.

There is a third possibility:  A _captured culture_ mode which at startup creates a string comparator based on the `CurrentCulture` in effect at system startup.  That is used by the `compare` function.  If you change `CurrentCulture`, it will not be seen by this; there is no thread-dependency.  You don't get the large speedup, but do get the 5% savings from the no-thread-lookup.  I'm not sure it is really worth it.  The audience would seem to be a culture-mode user who sorts heavily and doesn't care about threads.  I'm not sure that's much of a market.  (This would require a little more testing before full validation as an option. )

For ordinal mode users, varying culture in things such as sorting is still possible.  Most of the Clojure-defined functions, such as `sort` and `sorted-set`, have variations that take a comparator function. In fact, I think the general advice should be to be explicit in your sorting and comparator options always.   To make this easier, we will add a  `culture-comparator` function that will return a comparator function based either on the value of `CurrentCulture` when called or a supplied culture.  You could write `(sorted-set-by (culture-comparator "sv-SE"))`.

## The choices

The bug fixes will happen regardless.  For the `compare` changes, we have some choices.

1. Do nothing.  Not really an option, from my viewpoint.
2. Put in the switch.   Default is 'culture'.  If you want performance, you have to ask for it.  
3. Put in the switch.  Default is 'ordinal'.  Performance and consistency are the default.   

To be clear, my own preference is #3.  The current situation has internal inconsistencies, is inconsistent with the JVM, and is slower overall.  

An additional choice is

4. Do we need the 'captured culture' mode?

I plan to put a link to this document up on the #clr channel in the Clojurians Slack for feedback.  I'll amend this post to reflect that discussion.



## The result

TBD.



#### AI disclaimer/acknowledgement

I've been using AI coding tools extensively in my benchmarking work. There is a lot of tedious coding involved and the tools do it for me a lot faster and more correctly than I would on my own.  This document reports on the results of that work.  I drafted the document, then used the AI coding tool to vet it for accuracy, resulting in some typo corrections and a few suggested rewordings of inaccurate phrases.  A stray comment in its review of my first draft sent me down a new line of inquiry that ended up in the two-phase approach here.   The instructions to the agent include not drafting any code that might go into the ClojureCLR code base.  It can identify locations and make prose suggestions only.  (And it can write all the testing code its non-existent heart desires. It makes measurement runs on my commits as we go along.) 







