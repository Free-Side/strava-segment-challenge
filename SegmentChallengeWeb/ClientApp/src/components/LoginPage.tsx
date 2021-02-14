import * as React from "react";
import { ChangeEvent } from "react";
import { connect } from "react-redux";
import { ApplicationState } from "../store";
import * as LoginStore from "../store/Login";
import StravaLoginButton from "./StravaLoginButton";
import { Redirect } from "react-router";
import { onEnterKey } from "../shared/EventHelpers";
import { IQueryParamsProps, withQueryParams } from "../shared/WithQueryParams";

type LoginPageProps = {
    userEmail?: string,
    loginError?: string,
    loggedInUser?: LoginStore.LoginInfo
} & {
    usernamePasswordLogin: (credentials: { email: string, password: string }) => void
} & IQueryParamsProps;

type LoginPageState = {
    email: string,
    password: string,
    loggingIn: boolean,
    returnUrl?: string
};

class LoginPage extends React.PureComponent<LoginPageProps, LoginPageState> {
    constructor(props: LoginPageProps) {
        super(props);

        this.state = {
            email: props.userEmail ?? '',
            password: '',
            loggingIn: false,
            returnUrl: props.queryParams.get('returnUrl') ?? undefined,
        };

        this.handleEmailChanged = this.handleEmailChanged.bind(this);
        this.handlePasswordChanged = this.handlePasswordChanged.bind(this);
    }

    public render() {
        if (this.props.loggedInUser) {
            console.log(`Logged in. Redirecting to ${this.state.returnUrl || '/'}`);
            return <Redirect to={this.state.returnUrl || '/'} />;
        } else {
            return (
                <div className="login-options-container">
                    {/* TODO: Add Login with Google, Facebook */}
                    <StravaLoginButton returnUrl={this.state.returnUrl} />
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
)(withQueryParams(LoginPage));
