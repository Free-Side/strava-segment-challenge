import * as React from "react";
import { ChangeEvent } from "react";
import { connect } from "react-redux";
import { ApplicationState } from "../store";
import * as LoginStore from "../store/Login";
import StravaLoginButton from "./StravaLoginButton";
import { Redirect } from "react-router";
import { onEnterKey } from "../shared/EventHelpers";

type LoginPageProps = {
    userEmail?: string,
    loginError?: string,
    loggedInUser?: LoginStore.LoginInfo
} & {
    usernamePasswordLogin: (credentials: { email: string, password: string }) => void
};

type LoginPageState = {
    email: string,
    password: string,
    loggingIn: boolean
};

class LoginPage extends React.PureComponent<LoginPageProps, LoginPageState> {
    constructor(props: LoginPageProps) {
        super(props);

        this.state = {
            email: props.userEmail ?? '',
            password: '',
            loggingIn: false
        };

        this.handleEmailChanged = this.handleEmailChanged.bind(this);
        this.handlePasswordChanged = this.handlePasswordChanged.bind(this);
    }

    public render() {
        if (this.props.loggedInUser) {
            return <Redirect to="/" />;
        } else {
            return (
                <div className="login-options-container">
                    {/* TODO: Add Login with Google, Facebook */}
                    <StravaLoginButton />
                    <hr />
                    <form className="login-form">
                        <label>Email:
                            <input type="email" name="email" autoFocus={true} value={this.state.email} onChange={this.handleEmailChanged} />
                        </label>
                        <label>Password:
                            <input type="password"
                                   name="password"
                                   value={this.state.password}
                                   onChange={this.handlePasswordChanged}
                                   onKeyPress={onEnterKey(() => this.performLogin())} />
                        </label>
                        {this.props.loginError && <div className="error login-error">{this.props.loginError}</div>}
                        <button type="button" disabled={!(this.state.email && this.state.password)} onClick={() => this.performLogin()}>Log in</button>
                    </form>
                </div>
            );
        }
    }

    private handleEmailChanged(event: ChangeEvent<HTMLInputElement>) {
        this.setState({ email: event.target.value });
    }

    private handlePasswordChanged(event: ChangeEvent<HTMLInputElement>) {
        this.setState({ password: event.target.value });
    }

    private performLogin() {
        this.setState({ loggingIn: true });
        this.props.usernamePasswordLogin({ email: this.state.email, password: this.state.password });
    }
}

// TODO: track last login email via cookie or local storage
export default connect(
    (state: ApplicationState) => ({
        loggedInUser: state.login?.loggedInUser,
        loginError: state.login?.loginError
    }),
    LoginStore.actionCreators
)(LoginPage);
