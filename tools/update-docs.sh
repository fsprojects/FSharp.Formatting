#!/bin/bash
rm -rf .temp 
mkdir .temp
mkdir .temp/content
(cd tools && fsharpi build.fsx)
cp docs/output/*.html .temp
cp docs/output/content/* .temp/content
git checkout gh-pages
cp .temp/*.html .
cp .temp/content/* content/
git add *.html
git commit -m "Update generated documentation"
git push
git checkout master
rm -rf .temp 