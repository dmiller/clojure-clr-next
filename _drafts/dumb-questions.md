Dumb questions

================

A LazySeq is an ISeq.  An ASeq is an ISeq.  An ISeq is a Seqable.  
So the two special cases would be caught in the Sequable test in seqFrom.
Is the code structured this way because:
(1) In the case of ASeq, ASeq.seq() is final.  And very simple = return this;  Thus the compiler can inline.
(2) In the case of LazySeq, Lazy.seq() is final.  Can avoid ... something?  Given its complexity, it won't be inlined.  So just avoiding a vtable indirection of some kind?

BTW, this is a prime example of where I need something like protocols to avoid a massive circle of dependencies.
I need circular references to ASeq, LazySeq, chunkIteratorSeq, and a bunch of dependencies to make this work.

------------------

What is clojure.lang.MapEquivalence?

It is a marker interface.
APersistentMap implements it.
APersistentMap checks it in equiv.  Bascially if we have Dictionary (Map in Java) that is also an IPersistentMap but is not MapEquivalence, we won't bother checking to see if we are equivalent to you.  However, we will check against and arbitrary Dictionary (Map in Java).

The only other place it is mentioned is in core_deftype.clj.
the macroexpansion code for defrecord has the lines:

       (defn ~(symbol (str 'map-> gname))
         ~(str "Factory function for class " classname ", taking a map of keywords to field values.")
         ([m#] (~(symbol (str classname "/create"))
                (if (instance? clojure.lang.MapEquivalence m#) m# (into {} m#)))))
				
this is a factory function  of the form map->TypeName, taking a map of keywords to field values.	
We will take the supplied argument (m#) as is if it supports MapEquivalence, but copy it otherwise (so we will end up with a standard IPersistentMap that does support it).	
What situation are we dealing with here?

		
