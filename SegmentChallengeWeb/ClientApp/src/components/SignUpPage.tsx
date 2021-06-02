import * as React from "react";
import { ChangeEvent } from "react";
import { connect } from "react-redux";
import { ApplicationState } from "../store";
import * as LoginStore from "../store/Login";
import StravaLoginButton from "./StravaLoginButton";
import { Redirect } from "react-router";
import { Link } from "react-router-dom";
import { IQueryParamsProps, withQueryParams } from "../shared/WithQueryParams";

type SignUpPageProps = {
    signUpError?: string,
    loggedInUser?: LoginStore.LoginInfo
} & {
    signUp: (profile: LoginStore.UserSignUp) => void
} & IQueryParamsProps;

type SignUpPageState = {
    email?: string,
    password?: string,
    confirmPassword?: string,
    username?: string,
    firstName?: string,
    lastName?: string,
    year?: number,
    month?: number,
    day?: number,
    gender?: string,
    returnUrl?: string,
}

const months = [
    'Jan',
    'Feb',
    'Mar',
    'Apr',
    'May',
    'Jun',
    'Jul',
    'Aug',
    'Sep',
    'Oct',
    'Nov',
    'Dec'
];

const maxBirthYear = new Date().getFullYear() - 13;
const minBirthYear = maxBirthYear - (99 - 13);
let yearArray: number[] = [];
for (let y = maxBirthYear; y >= minBirthYear; y--) {
    yearArray.push(y);
}

function getDaysPerMonth(year: number | undefined, month: number | undefined) {
    if (month) {
        // Note month here is index by 1, where as javascript is index by zero, which is why this works.
        if (year) {
            return new Date(year, month, 0).getDate();
        } else {
            // Intentionally choosing a leap year, since we want to include 29 for february
            return new Date(2020, month, 0).getDate();
        }
    } else {
        return 31;
    }
}

class SignUpPage extends React.PureComponent<SignUpPageProps, SignUpPageState> {
    constructor(props: SignUpPageProps) {
        super(props);

        this.state = {
            email: '',
            password: '',
            confirmPassword: '',
            username: '',
            firstName: '',
            lastName: '',
            year: undefined,
            month: undefined,
            day: undefined,
            gender: undefined,
            returnUrl: props.queryParams.get('returnUrl') ?? undefined
        };

        this.handleEmailChanged = this.handleEmailChanged.bind(this);
        this.handlePasswordChanged = this.handlePasswordChanged.bind(this);
        this.handleConfirmPasswordChanged = this.handleConfirmPasswordChanged.bind(this);
        this.handleUsernameChanged = this.handleUsernameChanged.bind(this);
        this.handleFirstNameChanged = this.handleFirstNameChanged.bind(this);
        this.handleLastNameChanged = this.handleLastNameChanged.bind(this);
        this.handleYearChanged = this.handleYearChanged.bind(this);
        this.handleMonthChanged = this.handleMonthChanged.bind(this);
        this.handleDayChanged = this.handleDayChanged.bind(this);
        this.handleGenderChanged = this.handleGenderChanged.bind(this);
    }

    public render() {
        if (this.props.loggedInUser) {
            console.log('sign up successful. Redirecting.');
            return <Redirect to={this.state.returnUrl ?? '/'} />;
        } else {
            return (
                <div className="signup-page-container">
                    <div className="signup-options-container">
                        <h2>Option 1. Strava</h2>
                        <p className="signup-option-description"><em>If you have a Strava account please <strong>use this button to sign up using Strava</strong>. You will be redirected to the Strava website or app and prompted to authorize ASBRA to access your activity information. Once you click the authorize button you will be redirected back to this page where you will be prompted to enter some additional information.</em></p>
                        <StravaLoginButton signup={true} returnUrl={this.state.returnUrl} />
                        <hr />
                        <h2>Option 2. Email Address & Password</h2>
                        <p className="signup-option-description"><em>Only fill out and submit the form below if you <strong>do not have a Strava account.</strong></em></p>
                        <form className="signup-form">
                            <p>In order to participate in challenges we need to know a little bit about you.</p>
                            <div className="user-detail-row">
                                <label>Email <span className="material-icons"
                                                   title="Your email address will only be used for you to sign in, and to send you a confirmation of prizes at the end of the challenge. We will not share your information with third parties.">help_outline</span>:
                                    <input type="email"
                                           name="email"
                                           value={this.state.email}
                                           onChange={this.handleEmailChanged} />
                                </label>
                                <p className="form-field-description no-margin-bottom">You will use your Email to log in.</p>
                            </div>
                            <div className="user-detail-row">
                                <label>Password:
                                    <input type="password"
                                           name="password"
                                           value={this.state.password}
                                           onChange={this.handlePasswordChanged} />
                                </label>
                                <p className={`form-field-description no-margin-bottom ${(!this.state.password?.length || this.state.password?.length >= 7) ? '' : 'error'}`}>Your
                                    password must be at least 7 characters.</p>
                            </div>
                            <div className="user-detail-row">
                                <label>Confirm Password:
                                    <input type="password"
                                           name="confirmPassword"
                                           value={this.state.confirmPassword}
                                           onChange={this.handleConfirmPasswordChanged} />
                                </label>
                                {this.state.confirmPassword &&
                                this.state.confirmPassword != this.state.password &&
                                <p className="form-field-description error">The passwords must match exactly.</p>}

                            </div>
                            <div className="user-detail-row">
                                <label>Display Name:
                                    <input type="text"
                                           name="username"
                                           value={this.state.username}
                                           onChange={this.handleUsernameChanged} />
                                </label>
                                <p className="form-field-description no-margin-bottom">This name will be displayed in leader boards.</p>
                            </div>
                            <div className="user-detail-row">
                                <label>First Name:
                                    <input type="text"
                                           name="firstName"
                                           value={this.state.firstName}
                                           onChange={this.handleFirstNameChanged} />
                                </label>
                            </div>
                            <div className="user-detail-row">
                                <label>Last Name:
                                    <input type="text"
                                           name="lastName"
                                           value={this.state.lastName}
                                           onChange={this.handleLastNameChanged} />
                                </label>
                            </div>
                            <div className="user-detail-row birthdate-row">
                                <p className="form-field-description no-margin-top">Birthdate</p>
                                <label>Month:
                                    <select value={this.state.month} onChange={this.handleMonthChanged}>
                                        <option value={undefined}></option>
                                        {months.map((m, i) => <option key={i + 1} value={i + 1}>{m}</option>)}
                                    </select>
                                </label>
                                <label>Day:
                                    <select value={this.state.day} onChange={this.handleDayChanged}>
                                        <option value={undefined}></option>
                                        {[...Array(getDaysPerMonth(this.state.year, this.state.month))]
                                            .map((_, i) => <option key={i + 1} value={i + 1}>{i + 1}</option>)}
                                    </select>
                                </label>
                                <label>Year:
                                    <select value={this.state.year} onChange={this.handleYearChanged}>
                                        <option value={undefined}></option>
                                        {yearArray.map(y => <option key={y} value={y}>{y}</option>)}
                                    </select>
                                </label>
                            </div>
                            <div className="user-detail-row">
                                <label>Gender:
                                    <select value={this.state.gender} onChange={this.handleGenderChanged}>
                                        <option value={undefined}></option>
                                        <option value="M">Male</option>
                                        <option value="F">Female</option>
                                    </select>
                                </label>
                            </div>
                            {this.props.signUpError &&
                            <p className="error">{this.props.signUpError}</p>}
                            <button id="sign_up_button"
                                    type="button"
                                    disabled={!(this.state.email && this.state.password && (this.state.password === this.state.confirmPassword) && this.state.firstName && this.state.lastName && this.state.year && this.state.month && this.state.day && this.state.gender)}
                                    onClick={() => this.signUp()}>Sign Up
                            </button>
                        </form>
                    </div>
                    <div className="login-link-container">
                        Already signed up? <Link to={this.state.returnUrl ? `/login?returnUrl=${encodeURIComponent(this.state.returnUrl)}` : '/login'}>Log in instead.</Link>
                    </div>
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

    private handleConfirmPasswordChanged(event: ChangeEvent<HTMLInputElement>) {
        this.setState({ confirmPassword: event.target.value });
    }

    private handleUsernameChanged(event: ChangeEvent<HTMLInputElement>) {
        this.setState({ username: event.target.value });
    }

    private handleFirstNameChanged(event: ChangeEvent<HTMLInputElement>) {
        this.setState({ firstName: event.target.value });
    }

    private handleLastNameChanged(event: ChangeEvent<HTMLInputElement>) {
        this.setState({ lastName: event.target.value });
    }

    private handleYearChanged(event: ChangeEvent<HTMLSelectElement>) {
        this.setState({ year: Number(event.target.value) });
    }

    private handleMonthChanged(event: ChangeEvent<HTMLSelectElement>) {
        this.setState({ month: Number(event.target.value) });
    }

    private handleDayChanged(event: ChangeEvent<HTMLSelectElement>) {
        this.setState({ day: Number(event.target.value) });
    }

    private handleGenderChanged(event: ChangeEvent<HTMLSelectElement>) {
        this.setState({ gender: event.target.value });
    }

    private signUp() {
        if (this.state.email &&
            this.state.password &&
            this.state.password === this.state.confirmPassword &&
            this.state.username &&
            this.state.year &&
            this.state.month &&
            this.state.day &&
            this.state.gender) {
            this.props.signUp({
                email: this.state.email,
                password: this.state.password,
                username: this.state.username,
                firstName: this.state.firstName,
                lastName: this.state.lastName,
                birthDate: new Date(this.state.year, this.state.month - 1, this.state.day),
                gender: this.state.gender,
            });
        }
    }
}

export default connect(
    (state: ApplicationState) => ({ loggedInUser: state.login?.loggedInUser, signUpError: state.login?.signUpError }),
    LoginStore.actionCreators)(withQueryParams(SignUpPage));
