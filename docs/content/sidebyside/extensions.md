Markdown extensions
===================

To use LaTex extension, you need add javascript
link to [MathJax](http://www.mathjax.org/) in
your template.

To use inline LaTex, eclose LaTex code with `$`:
$ k_{n+1} = n^2 + k_n^2 - k_{n-1} $. Alternatively,
you can also use `$$`.

To use block LaTex, start a new parapgraph, with
the first line marked as `$$$` (no close `$$$`):

$$$
A_{m,n} =
 \begin{pmatrix}
  a_{1,1} & a_{1,2} & \cdots & a_{1,n} \\
  a_{2,1} & a_{2,2} & \cdots & a_{2,n} \\
  \vdots  & \vdots  & \ddots & \vdots  \\
  a_{m,1} & a_{m,2} & \cdots & a_{m,n}
 \end{pmatrix}

Use LaTex escape rule:

- Escape \$ in inline mode: $ \$ $, $ \$var $
- Other escapes: $ \& \% \$ \# \_ \{ \} $
- Using < or >: $ x > 1 $, $ y < 1 $, $ x >= 1 $,
  $ y <= 1 $, $ x = 1 $
- $ <p>something</p> $


