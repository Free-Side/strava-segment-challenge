#! /bin/bash

VERSION="$(cat VERSION)"

docker tag segment-challenge:$VERSION us.gcr.io/free-side-software/segment-challenge
docker tag segment-challenge:$VERSION us.gcr.io/free-side-software/segment-challenge:$VERSION
docker push us.gcr.io/free-side-software/segment-challenge
docker push us.gcr.io/free-side-software/segment-challenge:$VERSION
