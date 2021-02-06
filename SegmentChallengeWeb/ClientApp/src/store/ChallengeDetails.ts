import * as ChallengeListStore from "./ChallengeList";
import {AppThunkAction} from "./index";
import {AnyAction, Reducer} from "redux";
import {generateErrorMessage, hasContent} from "../RestHelper";

export interface AgeGroup {
    maximumAge: number,
    description: string
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
    isKOM: boolean
}

export interface Athlete {
    id: number,
    displayName: string,
    gender: string,
    age: number
}

export type Category = { minimumAge?: number, maximumAge?: number, gender?: string, description: string };

export interface ChallengeDetailsState {
    selectedChallengeName?: string,
    currentChallenge?: ChallengeListStore.Challenge,
    isAthleteRegistered?: boolean,
    selectedCategory: Category,
    ageGroups?: AgeGroup[],
    allEfforts?: Effort[],
    allAthletes?: Athlete[],
    errorMessage?: string
}


export enum ChallengeDetailActions {
    SelectedChallengeChanged = 'SELECTED_CHALLENGE_CHANGED',
    CurrentChallengeChanged = 'CURRENT_CHALLENGE_CHANGED',
    SelectedCategoryChanged = 'SELECTED_CATEGORY_CHANGED',
    RegistrationStatusReceived = 'REGISTRATION_STATUS_RECEIVED',
    AgeGroupsReceived = 'AGE_GROUPS_RECEIVED',
    EffortsReceived = 'EFFORTS_RECEIVED',
    AthletesReceived = 'ATHLETES_RECEIVED',
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
    | RegistrationStatusReceived
    | AgeGroupsReceived
    | EffortsReceived
    | AthletesReceived
    | ServerRequestError
    | ChallengeListStore.RequestChallengeListAction
    | ChallengeListStore.ReceiveChallengeListAction
    | ChallengeListStore.ErrorFetchingChallengeListAction;

function getSelectedChallenge(challenges: ChallengeListStore.Challenge[], selectedChallenge: string): ChallengeListStore.Challenge | undefined {
    return challenges.filter(c => c.name === selectedChallenge)[0];
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
                    // Fetch Registration Status
                    fetch(`${appState.config.apiBaseUrl}api/challenges/${selectedChallenge}/registration`, {credentials: 'same-origin'})
                        .then(async response => {
                            if (response.ok) {
                                const data = await response.json();
                                dispatch({
                                    type: ChallengeDetailActions.RegistrationStatusReceived,
                                    selectedChallengeName: selectedChallenge,
                                    registrationStatus: data.registered
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
    selectedCategoryChanged: (selectedCategory: Category) => ({
        type: ChallengeDetailActions.SelectedCategoryChanged,
        selectedCategory: selectedCategory
    } as SelectedCategoryChanged),
    joinChallenge: (): AppThunkAction<KnownAction> => (dispatch, getState) => {
        let appState = getState();

        if (appState) {
            const selectedChallenge = appState.challengeDetails?.selectedChallengeName;
            if (selectedChallenge && appState.login?.loggedInUser) {
                fetch(`${appState.config.apiBaseUrl}api/challenges/${selectedChallenge}/register`, {method: 'POST', credentials: 'same-origin'})
                    .then(async response => {
                        if (response.ok) {
                            const data = await response.json();
                            dispatch({
                                type: ChallengeDetailActions.RegistrationStatusReceived,
                                selectedChallengeName: selectedChallenge,
                                registrationStatus: data.registered
                            });

                            // Immediately initiate a refresh
                            fetch(`${appState.config.apiBaseUrl}api/challenges/${selectedChallenge}/refresh`, {method: 'POST', credentials: 'same-origin'})
                                .then(response => {
                                    if (response.ok) {
                                        console.log('Refresh started.');
                                    } else {
                                        console.log(`Refresh failed to start: ${response.status}`)
                                    }
                                });
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
    }
};

const initialState: ChallengeDetailsState = {selectedCategory: {description: 'Overall'}};

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
                return {...state, currentChallenge: (action as CurrentChallengeChanged).currentChallenge};
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
            case ChallengeDetailActions.RegistrationStatusReceived:
                const registrationStatusReceived = action as RegistrationStatusReceived;
                if (registrationStatusReceived.selectedChallengeName === state.selectedChallengeName) {
                    return {...state, isAthleteRegistered: registrationStatusReceived.registrationStatus};
                }

                break;
            case ChallengeDetailActions.AgeGroupsReceived:
                const ageGroupsReceivedAction = action as AgeGroupsReceived;
                if (ageGroupsReceivedAction.selectedChallengeName === state.selectedChallengeName) {
                    return {...state, ageGroups: ageGroupsReceivedAction.ageGroups};
                }

                break;
            case ChallengeDetailActions.EffortsReceived:
                const effortsReceivedAction = action as EffortsReceived;
                if (effortsReceivedAction.selectedChallengeName === state.selectedChallengeName) {
                    return {...state, allEfforts: effortsReceivedAction.efforts};
                }

                break;
            case ChallengeDetailActions.AthletesReceived:
                const athletesReceivedAction = action as AthletesReceived;
                if (athletesReceivedAction.selectedChallengeName === state.selectedChallengeName) {
                    return {...state, allAthletes: athletesReceivedAction.athletes};
                }

                break;
            case ChallengeDetailActions.ServerRequestError:
            case "ERROR_FETCHING_CHALLENGE_LIST":
                return {...state, errorMessage: (action as HasMessage).message};
        }

        return state;
    };
