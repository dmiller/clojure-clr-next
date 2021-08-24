---
layout: post
title: Making a hash of things
date: 2021-08-26 19:36:00 -0500
categories: general
---

# Making a hash of things

The most complex data structure in the Clojure catalog has to be the PersistentHashMap. This is Phil Bagwell's [Hash Array Mapped Trie (HAMT)](https://en.wikipedia.org/wiki/Hash_array_mapped_trie) as modified by Rich Hickey to be immutable and persistent.  (Bagwell's original paper is available [here](http://infoscience.epfl.ch/record/64398/files/idealhashtrees.pdf).

There are plenty of tutorials available online with wonderful pictures and animations that illustrate the ideas behind persistent, immutable tree structures.  I refer you to them for nice visuals.  however, I'll provide some mediocre visuals.  The main idea is that we do not modify the parts making up the tree structure.  If a connection has to change, or the data in a node has to change, one makes a copy of the nodes from the changed node back up to the root, reproducing the structure of the part of the tree that does not change by linking to those parts from the new parts.  


