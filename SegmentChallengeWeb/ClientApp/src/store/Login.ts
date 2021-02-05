import {Action, AnyAction, Reducer} from 'redux';
import {AppThunkAction} from "./index";
import {generateErrorMessage, hasContent} from "../RestHelper";
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
    loggedInUser: LoginInfo | undefined,
    errorSettingProfile?: string
}

export interface LoggedInAction {
    type: 'LOGGED_IN',
    loggedInUser: LoginInfo
}

export interface ErrorSettingProfileAction {
    type: 'ERROR_SETTING_USER_PROFILE',
    message: string
}

function isLoggedInAction(action: Action): action is LoggedInAction {
    return action.type === 'LOGGED_IN';
}

function isErrorSettingProfileAction(action: Action): action is ErrorSettingProfileAction {
    return action.type === 'ERROR_SETTING_USER_PROFILE';
}

type KnownAction = LoggedInAction | ErrorSettingProfileAction

export const actionCreators = {
    setUserProfile: (profile: { birthDate: Date, gender: string, email?: string }): AppThunkAction<KnownAction> => (dispatch, getState) => {
        fetch(
            'api/athletes/self',
            {
                method: 'POST',
                credentials: 'same-origin',
                body: JSON.stringify(profile),
                headers: {'Content-Type': 'application/json'}
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
    }
};

const initialState = {loggedInUser: undefined} as LoginState;

export const reducer: Reducer<LoginState> = (state: LoginState | undefined, action: AnyAction) => {
    state = state || initialState;

    if (isLoggedInAction(action)) {
        return {...state, loggedInUser: action.loggedInUser};
    } else if (isErrorSettingProfileAction(action)) {
        return {...state, errorSettingProfile: action.message};
    }

    return state;
};
