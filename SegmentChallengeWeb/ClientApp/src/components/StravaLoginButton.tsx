import * as React from 'react';
import { connect } from 'react-redux';
import { ConfigurationState } from "../store/Configuration";
import { ApplicationState } from "../store";

const StravaLoginButton = (props: { config: ConfigurationState, signup?: boolean }) => (
    <a href={`${props.config.apiBaseUrl}api/connect/login`} className="link-button strava-login">
        { props.signup ? 'Sign up' : 'Log in'} with Strava
    </a>
);

export default connect((state: ApplicationState, props) =>
    ({ ...props, config: state.config }))(StravaLoginButton);
