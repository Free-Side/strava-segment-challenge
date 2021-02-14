#! /bin/bash

git commit -a -m "Release v$(cat VERSION)" && git tag "v$(cat VERSION)"
