import * as React from 'react';
import { connect } from 'react-redux';

const LoginButton = () => (
    <a href="/api/connect/login" className="strava-login">Login With Strava</a>
);

export default connect()(LoginButton);
