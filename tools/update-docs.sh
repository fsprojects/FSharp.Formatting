#!/bin/bash
rm -rf .temp 
mkdir .temp
fsharpi build.fsx
cp docs/output/*.html .temp
git checkout gh-pages
cp .temp/*.html .
git add *.html
git commit -m "Update generated documentation"
git push
git checkout master
rm -rf .temp 