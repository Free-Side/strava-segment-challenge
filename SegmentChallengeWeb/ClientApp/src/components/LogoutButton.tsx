import * as React from 'react';
import { connect } from 'react-redux';

const LogoutButton = () => (
    <a href="/api/connect/logout" className="strava-logout">Logout</a>
);

export default connect()(LogoutButton);
