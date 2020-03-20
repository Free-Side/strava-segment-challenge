import React from 'react';
import {Route} from 'react-router';
import Layout from './components/Layout';
import Home from './components/Home';
import ChallengeDetails from "./components/ChallengeDetails";

import './site.scss'

export default () => (
    <Layout>
        <Route exact path="/" component={Home}/>
        <Route exact path="/challenge/:challengeName?" component={ChallengeDetails}/>
    </Layout>
);
