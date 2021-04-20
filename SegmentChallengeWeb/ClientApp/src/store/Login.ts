import { Action, AnyAction, Reducer } from 'redux';
import { AppThunkAction } from "./index";
import { generateErrorMessage, hasContent } from "../RestHelper";
import * as RestHelper from '../RestHelper';

export interface LoginInfo {
    sub: string,
    name: string,
    user_data: { profile_picture?: string, birth_date?: string, gender?: string, email?: string, is_admin?: boolean },
    nbf: number,
    exp: number,
    iat: number,
    iss: string,
    aud: string
}

export interface LoginState {
    loggedInUser?: LoginInfo,
    loginError?: string,
    signUpError?: string,
    errorSettingProfile?: string
}

export interface LoggedInAction {
    type: 'LOGGED_IN',
    loggedInUser: LoginInfo
}

export interface LoggedOutAction {
    type: 'LOGGED_OUT'
}

export interface ErrorLoggingIn {
    type: 'ERROR_LOGGING_IN',
    message: string
}

export interface ErrorSigningUp {
    type: 'ERROR_SIGNING_UP',
    message: string
}

export interface ErrorSettingProfileAction {
    type: 'ERROR_SETTING_USER_PROFILE',
    message: string
}

export enum LoginActions {
    LoggedIn = 'LOGGED_IN',
    LoggedOut = 'LOGGED_OUT',
    ErrorLoggingIn = 'ERROR_LOGGING_IN',
    ErrorSigningUp = 'ERROR_SIGNING_UP',
    ErrorSettingProfile = 'ERROR_SETTING_USER_PROFILE'
}

export interface UserProfile {
    birthDate: Date;
    gender: string;
    email?: string;
}

export interface UserSignUp {
    email: string;
    password: string;
    username: string;
    firstName?: string;
    lastName?: string;
    birthDate: Date;
    gender: string;
}

function isLoggedInAction(action: Action): action is LoggedInAction {
    return action.type === 'LOGGED_IN';
}

function isLoggedOutAction(action: Action): action is LoggedInAction {
    return action.type === 'LOGGED_OUT';
}

function isErrorLoggingIn(action: Action): action is ErrorLoggingIn {
    return action.type === 'ERROR_LOGGING_IN';
}

function isErrorSigningUp(action: Action): action is ErrorSigningUp {
    return action.type === 'ERROR_SIGNING_UP';
}

function isErrorSettingProfileAction(action: Action): action is ErrorSettingProfileAction {
    return action.type === 'ERROR_SETTING_USER_PROFILE';
}

type KnownAction = LoggedInAction | ErrorLoggingIn | ErrorSigningUp | ErrorSettingProfileAction

export const actionCreators = {
    usernamePasswordLogin: (credentials: { email: string, password: string }): AppThunkAction<KnownAction> =>
        ((dispatch, getState) => {
            let appState = getState();
            console.log('performing username and password login for: ' + credentials.email);
            fetch(
                `${appState.config.apiBaseUrl}api/auth/login`,
                {
                    method: 'POST',
                    body: JSON.stringify(credentials),
                    headers: { 'Content-Type': 'application/json' }
                })
                .then(async response => {
                    if (response.ok) {
                        // check cookie
                        let loggedInUser = RestHelper.getLoggedInUser();

                        if (loggedInUser) {
                            dispatch({
                                type: 'LOGGED_IN',
                                loggedInUser
                            });
                        } else {
                            dispatch({
                                type: 'ERROR_LOGGING_IN',
                                message: 'Unexpected error attempting to log in'
                            });
                        }
                    } else {
                        console.log('Error logging in: ' + response.status);
                        dispatch({
                            type: 'ERROR_LOGGING_IN',
                            message: response.status >= 400 && response.status < 500 ?
                                'Invalid Email Address or Password' :
                                'Unexpected error attempting to log in'
                        });
                    }
                });
        }),
    signUp: (profile: UserSignUp): AppThunkAction<KnownAction> =>
        ((dispatch, getState) => {

            let appState = getState();
            fetch(
                `${appState.config.apiBaseUrl}api/auth/signup`,
                {
                    method: 'POST',
                    body: JSON.stringify(profile),
                    headers: { 'Content-Type': 'application/json' }
                })
                .then(async response => {
                    if (response.ok) {
                        // check cookie
                        let loggedInUser = RestHelper.getLoggedInUser();

                        if (loggedInUser) {
                            dispatch({
                                type: 'LOGGED_IN',
                                loggedInUser
                            });
                        } else {
                            dispatch({
                                type: 'ERROR_SIGNING_UP',
                                message: 'Unexpected error attempting to sign up.'
                            });
                        }
                    } else if (response.status === 400) {
                        let problem = await response.json();
                        dispatch({
                            type: 'ERROR_SIGNING_UP',
                            message: problem.detail
                        });
                    } else {
                        dispatch({
                            type: 'ERROR_SIGNING_UP',
                            message: 'Unexpected error attempting to sign up.'
                        });
                    }
                });
        }),
    setUserProfile: (profile: { birthDate: Date, gender: string, email?: string }): AppThunkAction<KnownAction> =>
        ((dispatch, getState) => {
            let appState = getState();
            fetch(
                `${appState.config.apiBaseUrl}api/athletes/self`,
                {
                    method: 'POST',
                    credentials: 'same-origin',
                    body: JSON.stringify(profile),
                    headers: { 'Content-Type': 'application/json' }
                })
                .then(async response => {
                    if (response.ok) {
                        // check cookie
                        let loggedInUser = RestHelper.getLoggedInUser();

                        if (loggedInUser) {
                            dispatch({
                                type: 'LOGGED_IN',
                                loggedInUser
                            });
                        } else {
                            dispatch({
                                type: 'ERROR_SETTING_USER_PROFILE',
                                message: 'Failed to update user profile.'
                            });
                        }
                    } else {
                        let detail: string;
                        if (hasContent(response)) {
                            detail = await response.text();
                        } else {
                            detail = 'Unknown';
                        }

                        console.error(`Error updating user profile. Status: ${response.status}, Status Description: ${response.statusText}, Detail: ${detail}`);

                        dispatch({
                            type: 'ERROR_SETTING_USER_PROFILE',
                            message: generateErrorMessage('user profile', response.status, response.statusText, detail)
                        });
                    }
                })
                .catch(error => {
                    dispatch({
                        type: 'ERROR_SETTING_USER_PROFILE',
                        message: error
                    })
                });
        })
};

const initialState = { loggedInUser: undefined } as LoginState;

export const reducer: Reducer<LoginState> = (state: LoginState | undefined, action: AnyAction) => {
    state = state || initialState;

    if (isLoggedInAction(action)) {
        return { ...state, loggedInUser: action.loggedInUser, loginError: undefined, signUpError: undefined };
    } else if (isLoggedOutAction(action)) {
        return { ...state, loggedInUser: undefined, loginError: undefined, signUpError: undefined };
    } else if (isErrorLoggingIn(action)) {
        console.log('Error logging in: ' + action.message);
        return { ...state, loggedInUser: undefined, loginError: action.message };
    } else if (isErrorSigningUp(action)) {
        return { ...state, signUpError: action.message };
    } else if (isErrorSettingProfileAction(action)) {
        return { ...state, errorSettingProfile: action.message };
    }

    return state;
};
