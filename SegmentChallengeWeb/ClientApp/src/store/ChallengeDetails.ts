import * as ChallengeListStore from "./ChallengeList";
import { ApplicationState, AppThunkAction } from "./index";
import { AnyAction, Reducer } from "redux";
import { generateErrorMessage, hasContent } from "../RestHelper";
import { LoggedInAction, LoggedOutAction, LoginActions } from "./Login";

export interface AgeGroup {
    maximumAge: number,
    description: string
}

export interface SpecialCategory {
    specialCategoryId: number,
    categoryName: string
}

export interface Effort {
    id: number,
    athleteId: number,
    athleteName: number
    athleteGender: string,
    athleteAge: number,
    activityId: number,
    lapCount: number,
    elapsedTime: number,
    isKOM: boolean,
    specialCategoryId: number | null
}

export interface Athlete {
    id: number,
    displayName: string,
    gender: string,
    age: number,
    specialCategoryId: number | null
}

export type Category = { minimumAge?: number, maximumAge?: number, gender?: string, description: string };

export interface ChallengeDetailsState {
    selectedChallengeName?: string,
    currentChallenge?: ChallengeListStore.Challenge,
    isAthleteRegistered?: boolean,
    inviteCode?: string,
    waitingForInviteCode?: boolean,
    registering?: boolean,
    registrationError?: string,
    selectedCategory: Category | null,
    ageGroups?: AgeGroup[],
    specialCategories?: SpecialCategory[],
    allEfforts?: Effort[],
    allAthletes?: Athlete[],
    errorMessage?: string,
    fetchingRegistrationStatusFor?: string,
}

export enum ChallengeDetailActions {
    SelectedChallengeChanged = 'SELECTED_CHALLENGE_CHANGED',
    CurrentChallengeChanged = 'CURRENT_CHALLENGE_CHANGED',
    SelectedCategoryChanged = 'SELECTED_CATEGORY_CHANGED',
    FetchingRegistrationStatus = 'FETCHING_REGISTRATION_STATUS',
    RegistrationStatusReceived = 'REGISTRATION_STATUS_RECEIVED',
    AgeGroupsReceived = 'AGE_GROUPS_RECEIVED',
    SpecialCategoriesReceived = 'SPECIAL_CATEGORIES_RECEIVED',
    EffortsReceived = 'EFFORTS_RECEIVED',
    AthletesReceived = 'ATHLETES_RECEIVED',
    SpecifyInviteCode = 'SPECIFY_INVITE_CODE',
    Registering = 'REGISTERING',
    InviteCodeChanged= 'INVITE_CODE_CHANGED',
    InvalidInviteCode = 'INVALID_INVITE_CODE',
    CancelJoin = 'CANCEL_JOIN',
    ServerRequestError = 'ERROR_FETCHING_DATA'
}

export interface SelectedChallengeChanged {
    type: ChallengeDetailActions.SelectedChallengeChanged,
    selectedChallengeName: string | undefined
}

export interface SelectedCategoryChanged {
    type: ChallengeDetailActions.SelectedCategoryChanged,
    selectedCategory: Category
}

export interface CurrentChallengeChanged {
    type: ChallengeDetailActions.CurrentChallengeChanged,
    currentChallenge: ChallengeListStore.Challenge | undefined
}

export interface FetchingRegistrationStatus {
    type: ChallengeDetailActions.FetchingRegistrationStatus,
    selectedChallengeName: string,
}

export interface RegistrationStatusReceived {
    type: ChallengeDetailActions.RegistrationStatusReceived,
    selectedChallengeName: string,
    registrationStatus: boolean
}

export interface AgeGroupsReceived {
    type: ChallengeDetailActions.AgeGroupsReceived,
    selectedChallengeName: string,
    ageGroups: AgeGroup[]
}

export interface SpecialCategoriesReceived {
    type: ChallengeDetailActions.SpecialCategoriesReceived,
    selectedChallengeName: string,
    specialCategories: SpecialCategory[]
}

export interface EffortsReceived {
    type: ChallengeDetailActions.EffortsReceived,
    selectedChallengeName: string,
    efforts: Effort[]
}

export interface AthletesReceived {
    type: ChallengeDetailActions.AthletesReceived,
    selectedChallengeName: string,
    athletes: Athlete[]
}

export interface SpecifyInviteCode {
    type: ChallengeDetailActions.SpecifyInviteCode
}

export interface Registering {
    type: ChallengeDetailActions.Registering
}

export interface InviteCodeChanged {
    type: ChallengeDetailActions.InviteCodeChanged;
    inviteCode: string;
}

export interface InvalidInviteCode {
    type: ChallengeDetailActions.InvalidInviteCode
}

export interface CancelJoin {
    type: ChallengeDetailActions.CancelJoin
}

export interface HasMessage {
    message: string
}

export interface ServerRequestError extends HasMessage {
    type: ChallengeDetailActions.ServerRequestError
}

type KnownAction =
    SelectedChallengeChanged
    | CurrentChallengeChanged
    | SelectedCategoryChanged
    | FetchingRegistrationStatus
    | RegistrationStatusReceived
    | AgeGroupsReceived
    | SpecialCategoriesReceived
    | EffortsReceived
    | AthletesReceived
    | SpecifyInviteCode
    | Registering
    | InviteCodeChanged
    | InvalidInviteCode
    | CancelJoin
    | ServerRequestError
    | ChallengeListStore.RequestChallengeListAction
    | ChallengeListStore.ReceiveChallengeListAction
    | ChallengeListStore.ErrorFetchingChallengeListAction
    | LoggedInAction
    | LoggedOutAction;

function getSelectedChallenge(challenges: ChallengeListStore.Challenge[], selectedChallenge: string): ChallengeListStore.Challenge | undefined {
    return challenges.filter(c => c.name === selectedChallenge)[0];
}

function fetchRegistrationStatus(appState: ApplicationState, selectedChallenge: string, dispatch: (action: KnownAction) => void) {
    if (appState.challengeDetails?.fetchingRegistrationStatusFor === selectedChallenge) {
        // Don't fetch multiple times
        return;
    }

    // Fetch Registration Status
    dispatch({
        type: ChallengeDetailActions.FetchingRegistrationStatus,
        selectedChallengeName: selectedChallenge
    })
    fetch(`${appState.config.apiBaseUrl}api/challenges/${selectedChallenge}/registration`, { credentials: 'same-origin' })
        .then(async response => {
            if (response.ok) {
                const data = await response.json();
                dispatch({
                    type: ChallengeDetailActions.RegistrationStatusReceived,
                    selectedChallengeName: selectedChallenge,
                    registrationStatus: data.registered
                });
            } else if (response.status === 401) {
                dispatch({
                    type: 'LOGGED_OUT'
                });
            } else {
                let detail: string;
                if (hasContent(response)) {
                    detail = await response.text();
                } else {
                    detail = 'Unknown';
                }

                console.error(`Error fetching registration status. Status: ${response.status}, Status Description: ${response.statusText}, Detail: ${detail}`);

                dispatch({
                    type: ChallengeDetailActions.ServerRequestError,
                    message: generateErrorMessage('registration status', response.status, response.statusText, detail)
                });
            }
        })
        .catch(error => {
            dispatch({
                type: ChallengeDetailActions.ServerRequestError,
                message: error
            })
        });
}

export const actionCreators = {
    onSelectedChallengeChanged: (selectedChallenge: string): AppThunkAction<KnownAction> => (dispatch, getState) => {
        let appState = getState();

        if (appState) {
            if (!appState.challengeDetails || selectedChallenge !== appState.challengeDetails.selectedChallengeName) {
                dispatch({
                    type: ChallengeDetailActions.SelectedChallengeChanged,
                    selectedChallengeName: selectedChallenge
                });

                if (!appState.challengeList || !appState.challengeList.challenges) {
                    // Need to wait for challenge list
                    ChallengeListStore.actionCreators.requestChallengeList()(dispatch, getState);
                } else {
                    // The easy way, we already have the challenge list
                    dispatch({
                        type: ChallengeDetailActions.CurrentChallengeChanged,
                        currentChallenge: getSelectedChallenge(appState.challengeList.challenges, selectedChallenge)
                    });
                }

                if (appState.login?.loggedInUser) {
                    fetchRegistrationStatus(appState, selectedChallenge, dispatch);
                }

                // Fetch Age Groups
                fetch(`${appState.config.apiBaseUrl}api/challenges/${selectedChallenge}/age_groups`)
                    .then(async response => {
                        if (response.ok) {
                            const data = await response.json();
                            dispatch({
                                type: ChallengeDetailActions.AgeGroupsReceived,
                                selectedChallengeName: selectedChallenge,
                                ageGroups: data
                            });
                        } else {
                            let detail: string;
                            if (hasContent(response)) {
                                detail = await response.text();
                            } else {
                                detail = 'Unknown';
                            }

                            console.error(`Error fetching age groups. Status: ${response.status}, Status Description: ${response.statusText}, Detail: ${detail}`);

                            dispatch({
                                type: ChallengeDetailActions.ServerRequestError,
                                message: generateErrorMessage('challenge age groups', response.status, response.statusText, detail)
                            });
                        }
                    })
                    .catch(error => {
                        dispatch({
                            type: ChallengeDetailActions.ServerRequestError,
                            message: error
                        })
                    });

                // Fect Special Categories
                fetch(`${appState.config.apiBaseUrl}api/challenges/${selectedChallenge}/special_categories`)
                    .then(async response => {
                        if (response.ok) {
                            const data = await response.json();
                            dispatch({
                                type: ChallengeDetailActions.SpecialCategoriesReceived,
                                selectedChallengeName: selectedChallenge,
                                specialCategories: data
                            });
                        } else {
                            let detail: string;
                            if (hasContent(response)) {
                                detail = await response.text();
                            } else {
                                detail = 'Unknown';
                            }

                            console.error(`Error fetching special categories. Status: ${response.status}, Status Description: ${response.statusText}, Detail: ${detail}`);

                            dispatch({
                                type: ChallengeDetailActions.ServerRequestError,
                                message: generateErrorMessage('challenge categories', response.status, response.statusText, detail)
                            });
                        }
                    })
                    .catch(error => {
                        dispatch({
                            type: ChallengeDetailActions.ServerRequestError,
                            message: error
                        })
                    });

                // Fetch Efforts
                fetch(`${appState.config.apiBaseUrl}api/challenges/${selectedChallenge}/efforts`)
                    .then(async response => {
                        if (response.ok) {
                            const data = await response.json();
                            dispatch({
                                type: ChallengeDetailActions.EffortsReceived,
                                selectedChallengeName: selectedChallenge,
                                efforts: data
                            });
                        } else {
                            let detail: string;
                            if (hasContent(response)) {
                                detail = await response.text();
                            } else {
                                detail = 'Unknown';
                            }

                            console.error(`Error fetching efforts. Status: ${response.status}, Status Description: ${response.statusText}, Detail: ${detail}`);

                            dispatch({
                                type: ChallengeDetailActions.ServerRequestError,
                                message: generateErrorMessage('efforts', response.status, response.statusText, detail)
                            });
                        }
                    })
                    .catch(error => {
                        dispatch({
                            type: ChallengeDetailActions.ServerRequestError,
                            message: error
                        })
                    });

                // Fetch Athletes
                fetch(`${appState.config.apiBaseUrl}api/challenges/${selectedChallenge}/athletes`)
                    .then(async response => {
                        if (response.ok) {
                            const data = await response.json();
                            dispatch({
                                type: ChallengeDetailActions.AthletesReceived,
                                selectedChallengeName: selectedChallenge,
                                athletes: data
                            });
                        } else {
                            let detail: string;
                            if (hasContent(response)) {
                                detail = await response.text();
                            } else {
                                detail = 'Unknown';
                            }

                            console.error(`Error fetching athletes. Status: ${response.status}, Status Description: ${response.statusText}, Detail: ${detail}`);

                            dispatch({
                                type: ChallengeDetailActions.ServerRequestError,
                                message: generateErrorMessage('athletes', response.status, response.statusText, detail)
                            });
                        }
                    })
                    .catch(error => {
                        dispatch({
                            type: ChallengeDetailActions.ServerRequestError,
                            message: error
                        })
                    });
            }
        }
    },
    refreshRegistrationStatus: (selectedChallenge: string): AppThunkAction<KnownAction> => (dispatch, getState) => {
        let appState = getState();

        if (appState.login?.loggedInUser) {
            fetchRegistrationStatus(appState, selectedChallenge, dispatch);
        }
    },
    selectedCategoryChanged: (selectedCategory: Category) => ({
        type: ChallengeDetailActions.SelectedCategoryChanged,
        selectedCategory: selectedCategory
    } as SelectedCategoryChanged),
    inviteCodeChanged: (inviteCode: string): AppThunkAction<KnownAction> => (dispatch, getState) => {
        dispatch({
            type: ChallengeDetailActions.InviteCodeChanged,
            inviteCode
        });
    },
    joinChallenge: (inviteCode?: string): AppThunkAction<KnownAction> => (dispatch, getState) => {
        let appState = getState();

        if (appState) {
            const selectedChallenge = appState.challengeDetails?.currentChallenge;
            if (selectedChallenge && appState.login?.loggedInUser) {
                if (selectedChallenge.requiresInviteCode && !inviteCode) {
                    dispatch({
                        type: ChallengeDetailActions.SpecifyInviteCode
                    });
                    return;
                }

                dispatch({
                    type: ChallengeDetailActions.Registering
                });

                let url = `${appState.config.apiBaseUrl}api/challenges/${selectedChallenge.name}/register`;
                if (inviteCode) {
                    url = `${url}?inviteCode=${inviteCode}`;
                }
                fetch(url, { method: 'POST', credentials: 'same-origin' })
                    .then(async response => {
                        if (response.ok) {
                            const data = await response.json();
                            dispatch({
                                type: ChallengeDetailActions.RegistrationStatusReceived,
                                selectedChallengeName: selectedChallenge.name,
                                registrationStatus: data.registered
                            });

                            // Immediately initiate a refresh
                            fetch(`${appState.config.apiBaseUrl}api/challenges/${selectedChallenge.name}/refresh`, {
                                method: 'POST',
                                credentials: 'same-origin'
                            })
                                .then(response => {
                                    if (response.ok) {
                                        console.log('Refresh started.');
                                    } else {
                                        console.log(`Refresh failed to start: ${response.status}`)
                                    }
                                });
                        } else if (response.status === 400 && hasContent(response)) {
                            let problem = await response.json();
                            if (problem.type === "urn:segment-challenge-app:invalid-invite-code") {
                                dispatch({
                                    type: ChallengeDetailActions.InvalidInviteCode
                                });
                            } else {
                                console.error(`Error updating registration status. Status: ${response.status}, Status Description: ${response.statusText}, Detail: Unknown`);

                                dispatch({
                                    type: ChallengeDetailActions.ServerRequestError,
                                    message: generateErrorMessage('registration status', response.status, response.statusText, 'Unknown')
                                });
                            }
                        } else {
                            let detail: string;
                            if (hasContent(response)) {
                                detail = await response.text();
                            } else {
                                detail = 'Unknown';
                            }

                            console.error(`Error updating registration status. Status: ${response.status}, Status Description: ${response.statusText}, Detail: ${detail}`);

                            dispatch({
                                type: ChallengeDetailActions.ServerRequestError,
                                message: generateErrorMessage('registration status', response.status, response.statusText, detail)
                            });
                        }
                    })
                    .catch(error => {
                        dispatch({
                            type: ChallengeDetailActions.ServerRequestError,
                            message: error
                        })
                    });
            }
        }
    },
    cancelJoin: (): AppThunkAction<KnownAction> => (dispatch, getState) => {
        dispatch({
            type: ChallengeDetailActions.CancelJoin
        });
    }
};

const initialState: ChallengeDetailsState = { selectedCategory: { description: 'Overall' } };

export const reducer: Reducer<ChallengeDetailsState> =
    (state: ChallengeDetailsState | undefined, incomingAction: AnyAction) => {
        state = state || initialState;

        const action = incomingAction as KnownAction;

        switch (action.type) {
            case ChallengeDetailActions.SelectedChallengeChanged:
                // Is this ok? Do we have to null out things individually?
                return {
                    ...initialState,
                    selectedChallengeName: (action as SelectedChallengeChanged).selectedChallengeName,
                };
            case ChallengeDetailActions.CurrentChallengeChanged:
                return { ...state, currentChallenge: (action as CurrentChallengeChanged).currentChallenge };
            case 'RECEIVE_CHALLENGE_LIST':
                if (state.selectedChallengeName) {
                    return {
                        ...state,
                        currentChallenge:
                            getSelectedChallenge(
                                (action as ChallengeListStore.ReceiveChallengeListAction).challenges,
                                state.selectedChallengeName
                            )
                    };
                }

                break;
            case ChallengeDetailActions.FetchingRegistrationStatus:
                return {
                    ...state,
                    fetchingRegistrationStatusFor: (action as FetchingRegistrationStatus).selectedChallengeName
                };
            case ChallengeDetailActions.RegistrationStatusReceived:
                const registrationStatusReceived = action as RegistrationStatusReceived;
                if (registrationStatusReceived.selectedChallengeName === state.selectedChallengeName) {
                    return {
                        ...state,
                        isAthleteRegistered: registrationStatusReceived.registrationStatus,
                        waitingForInviteCode: state.waitingForInviteCode && !registrationStatusReceived.registrationStatus,
                        registrationError: undefined,
                        fetchingRegistrationStatusFor:
                            (state.fetchingRegistrationStatusFor === registrationStatusReceived.selectedChallengeName) ?
                                undefined :
                                state.fetchingRegistrationStatusFor,
                    };
                }

                break;
            case ChallengeDetailActions.AgeGroupsReceived:
                const ageGroupsReceivedAction = action as AgeGroupsReceived;
                if (ageGroupsReceivedAction.selectedChallengeName === state.selectedChallengeName) {
                    return { ...state, ageGroups: ageGroupsReceivedAction.ageGroups };
                }

                break;
            case ChallengeDetailActions.SpecialCategoriesReceived:
                const specialCategoriesReceivedAction = action as SpecialCategoriesReceived;
                if (specialCategoriesReceivedAction.selectedChallengeName === state.selectedChallengeName) {
                    return { ...state, specialCategories: specialCategoriesReceivedAction.specialCategories };
                }

                break;
            case ChallengeDetailActions.EffortsReceived:
                const effortsReceivedAction = action as EffortsReceived;
                if (effortsReceivedAction.selectedChallengeName === state.selectedChallengeName) {
                    return { ...state, allEfforts: effortsReceivedAction.efforts };
                }

                break;
            case ChallengeDetailActions.AthletesReceived:
                const athletesReceivedAction = action as AthletesReceived;
                if (athletesReceivedAction.selectedChallengeName === state.selectedChallengeName) {
                    return { ...state, allAthletes: athletesReceivedAction.athletes };
                }

                break;
            case ChallengeDetailActions.SpecifyInviteCode:
                return { ...state, waitingForInviteCode: true };
            case ChallengeDetailActions.Registering:
                return { ...state, registering: true };
            case ChallengeDetailActions.InviteCodeChanged:
                return { ...state, inviteCode: (action as InviteCodeChanged).inviteCode };
            case ChallengeDetailActions.InvalidInviteCode:
                return { ...state, registering: false, registrationError: 'Invalid invite code.', waitingForInviteCode: true };
            case ChallengeDetailActions.CancelJoin:
                return { ...state, waitingForInviteCode: false, inviteCode: undefined };
            case LoginActions.LoggedOut:
                return {
                    ...state,
                    registrationError: 'You have been logged out. Please login again to see your registration status.',
                    fetchingRegistrationStatusFor: undefined,
                    isAthleteRegistered: undefined
                };
            case ChallengeDetailActions.ServerRequestError:
            case 'ERROR_FETCHING_CHALLENGE_LIST':
                return { ...state, errorMessage: (action as HasMessage).message };
        }

        return state;
    };
