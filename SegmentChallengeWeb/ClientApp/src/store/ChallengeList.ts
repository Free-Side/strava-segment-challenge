import { AnyAction, Reducer } from 'redux';
import { AppThunkAction } from './index';
import { generateErrorMessage, hasContent } from '../RestHelper'

export interface Challenge {
    id: number,
    name: string,
    displayName: string,
    description: string,
    segmentId: string,
    startDate: Date,
    endDate: Date,
    type: ChallengeType,
    useMovingTime: boolean,
    requiresInviteCode: boolean,
    registrationLink: string,
    routeMapImage: string,
}

function toChallenge(data: any) {
    return {
        ...data,
        startDate: new Date(data.startDate),
        endDate: new Date(data.endDate),
        type: data.type as ChallengeType,
    };
}

export enum ChallengeType {
    Fastest = 0,
    MostLaps = 1
}

export interface ChallengeListState {
    challenges?: Challenge[],
    requestPending: boolean,
    requestError?: string
}

export interface RequestChallengeListAction {
    type: 'REQUEST_CHALLENGE_LIST';
}

export interface ReceiveChallengeListAction {
    type: 'RECEIVE_CHALLENGE_LIST';
    challenges: Challenge[];
}

export interface ErrorFetchingChallengeListAction {
    type: 'ERROR_FETCHING_CHALLENGE_LIST';
    message: string;
}

type KnownAction = RequestChallengeListAction | ReceiveChallengeListAction | ErrorFetchingChallengeListAction;

export const actionCreators = {
    requestChallengeList: (): AppThunkAction<KnownAction> => (dispatch, getState) => {
        const appState = getState();
        // If there is no request pending, and we have not previously fetched challenges, fetch the challenge list
        if (appState && (!appState.challengeList || (!appState.challengeList.requestPending && !appState.challengeList.challenges))) {
            fetch(`${appState.config.apiBaseUrl}api/challenges`)
                .then(async response => {
                    if (response.ok) {
                        const data = await response.json();
                        dispatch({ type: 'RECEIVE_CHALLENGE_LIST', challenges: (data as unknown[]).map(toChallenge) });
                    } else {
                        let detail: string;
                        if (hasContent(response)) {
                            detail = await response.text();
                        } else {
                            detail = 'Unknown';
                        }

                        console.error(`Error fetching challenge list. Status: ${response.status}, Status Description: ${response.statusText}, Detail: ${detail}`);

                        dispatch({
                            type: 'ERROR_FETCHING_CHALLENGE_LIST',
                            message: generateErrorMessage('challenge list', response.status, response.statusText, detail)
                        });
                    }
                })
                .catch(error => {
                    dispatch({
                        type: 'ERROR_FETCHING_CHALLENGE_LIST',
                        message: error
                    })
                });

            dispatch({ type: 'REQUEST_CHALLENGE_LIST' });
        }
    }
};

const initialState: ChallengeListState = { requestPending: false };

export const reducer: Reducer<ChallengeListState> = (state: ChallengeListState | undefined, incomingAction: AnyAction): ChallengeListState => {
    state = state || initialState;

    const action = incomingAction as KnownAction;
    switch (action.type) {
        case 'REQUEST_CHALLENGE_LIST':
            if (!state.requestPending && !state.challenges) {
                return { ...state, requestPending: true };
            } else {
                // Ignore redundant requests
                return state;
            }
        case 'RECEIVE_CHALLENGE_LIST':
            return {
                ...state,
                requestPending: false,
                challenges: (incomingAction as ReceiveChallengeListAction).challenges
            };
        case 'ERROR_FETCHING_CHALLENGE_LIST':
            return {
                ...state,
                requestPending: false,
                challenges: [],
                requestError: (incomingAction as ErrorFetchingChallengeListAction).message
            };
        default:
            return state;
    }
};
