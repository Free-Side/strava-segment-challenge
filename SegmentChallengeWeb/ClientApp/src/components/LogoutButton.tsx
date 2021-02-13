import * as React from 'react';
import { connect } from 'react-redux';
import { ApplicationState } from "../store";
import { ConfigurationState } from "../store/Configuration";

const LogoutButton = (props: { config: ConfigurationState }) => (
    <a href={`${props.config.apiBaseUrl}api/connect/logout`} className="link-button strava-logout">Logout</a>
);

export default connect((state: ApplicationState, props) =>
    ({ ...props, config: state.config }))(LogoutButton);
