import * as React from 'react';
import { connect } from 'react-redux';
import { ConfigurationState } from "../store/Configuration";
import { ApplicationState } from "../store";

const StravaLoginButton = (props: { config: ConfigurationState, signup?: boolean, returnUrl?: string }) => {
    let url = (props.returnUrl ?
        `${props.config.apiBaseUrl}api/connect/login?returnUrl=${encodeURIComponent(props.returnUrl)}` :
        `${props.config.apiBaseUrl}api/connect/login`);
    return (
        <a href={url} className="link-button strava-login">
            {props.signup ? 'Sign up' : 'Log in'} with Strava
        </a>
    );
};

export default connect((state: ApplicationState, props) =>
    ({ ...props, config: state.config }))(StravaLoginButton);
