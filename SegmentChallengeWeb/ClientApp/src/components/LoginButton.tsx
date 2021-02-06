import * as React from 'react';
import { connect } from 'react-redux';
import { ConfigurationState } from "../store/Configuration";
import { ApplicationState } from "../store";

const LoginButton = (props: { config: ConfigurationState }) => (
    <a href={`${props.config.apiBaseUrl}api/connect/login`} className="strava-login">Login With Strava</a>
);

export default connect((state: ApplicationState, props) =>
    ({ ...props, config: state.config }))(LoginButton);
