import React from "react";
import { Route, Switch } from "react-router";
import Layout from "./components/Layout";
import Home from "./components/Home";
import ChallengeDetails from "./components/ChallengeDetails";
import LoginPage from "./components/LoginPage";
import NotFound from "./components/NotFound";
import SignUpPage from "./components/SignUpPage";

import "./site.scss"

export default () => (
    <Layout>
        <Switch>
            <Route exact path="/" component={Home} />
            <Route exact path="/login" component={LoginPage} />
            <Route exact path="/signup" component={SignUpPage} />
            <Route exact path="/challenge/:challengeName?" component={ChallengeDetails} />
            <Route component={NotFound} />
        </Switch>
    </Layout>
);
