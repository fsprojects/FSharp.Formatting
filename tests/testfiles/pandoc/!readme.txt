
iterates through all the test files in a given folder and generates file-based output 
this is essentially the same as running the unit tests, but with diff-able results

two files should be present for each test:

test_name.text         -- input (raw markdown)
test_name.html         -- output (expected cooked html output from reference markdown engine)

this file will be generated if, and ONLY IF, the expected output does not match the actual output:

test_name.xxxx.actual.html  -- actual output (actual cooked html output from our markdown c# engine)
                            -- xxxx is the 16-bit CRC checksum of the file contents; this is included
                               so you can tell if the contents of a failing test have changed

Contents
========

Pandoc edge condition tests from http://code.google.com/p/pandoc/wiki/PandocVsMarkdownPl