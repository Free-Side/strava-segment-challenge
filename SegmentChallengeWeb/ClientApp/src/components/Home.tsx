import * as React from 'react';
import {connect} from 'react-redux';
import {ApplicationState} from "../store";
import ChallengeList from "./ChallengeList";

const Home = (state: ApplicationState) => (
    <div>
        <h2>Challenges</h2>
        <ChallengeList />
    </div>
);

export default connect((state: ApplicationState) => state)(Home);
