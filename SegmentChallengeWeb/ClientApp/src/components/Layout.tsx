import * as React from 'react';
import {connect} from "react-redux";
import {Link} from "react-router-dom";
import LoginButton from "./LoginButton";
import LogoutButton from "./LogoutButton";
import GetUserDetails from "./GetUserDetails";
import {ApplicationState} from "../store";
import {ConfigurationState} from "../store/Configuration";
import {LoginState} from "../store/Login";

const Layout = (props: { children?: React.ReactNode, config: ConfigurationState, login?: LoginState }) => (
    <React.Fragment>
        <header>
            <div id="layout_title">
                <h1>
                    <Link to={'/'}>
                        {props.config.siteLogo &&
                        <img id="layout_logo" src={props.config.siteLogo} width="100px" height="100px" alt="logo"/>}
                        {props.config.siteTitle}
                    </Link>
                </h1>
            </div>
            <div id="layout_profile">
                {(props.login && props.login.loggedInUser) ?
                    <React.Fragment><span
                        id="layout_greeting">Welcome, {props.login.loggedInUser.name}</span><LogoutButton/></React.Fragment> :
                    <LoginButton/>}
            </div>
        </header>
        <section id="layout_main">
            {props.children}
        </section>
        {props.config.siteFooter && <footer>{props.config.siteFooter}</footer>}
        {/* If the user is logged in, but has not yet set their birth date and email, display the user profile dialog. */}
        {props.login?.loggedInUser && !(props.login.loggedInUser.user_data.birth_date && props.login.loggedInUser.user_data.email) &&
        <GetUserDetails />}
    </React.Fragment>
);

export default connect(
    (state: ApplicationState, props) =>
        ({...props, config: state.config, login: state.login}))(Layout);
