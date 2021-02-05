#! /bin/bash

VERSION="$(cat VERSION)"

docker build -t segment-challenge:$VERSION .
