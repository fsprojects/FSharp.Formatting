Dynamic programming 
===================================


Dynamic programming is equivalent to the folowwing two concepts used together : _recursion_ and _deforestation_


Recursion
---------

Recursion allows to _express_ solutions that bends itself to it in a simpler way. It is merely a way to formulate a solution in terms of a **subproblem graph** of diminishing size. by itself, it does not reduce the complexity of computing the solution.



Deforestation
-------------


If subproblems appears multiple times in the subproblem graph, then storing intermediary results allows for a  dynamic folding of it, replacing the graph with its computed value. This is where the improvement of the dynamic programming comes from.


Two strategies are possible for this : 

- a **bottom up** approach, evaluating adjacents sub problems in a reverse topological order, that is tackling simpler problem first and guaranteing that simpler problems are all solved before increasing complexity.
This solution has low overhead but special care must be taken to the order of evaluation

- a **top down** approach, where we add a memoization steps, and that keep the natural. This allows unnessary branch, if any, to not be computed.


Both approaches yield the same asymptotical performance.



Examples
--------

    let s = "Hello"

Those examples are taken from the book [Introduction to algorithm][cormen]

Rod cutting 

Matrix product

Longest Common subsequence

Optimal binary search tree

 [cormen]:http://en.wikipedia.org/wiki/Introduction_to_Algorithms
