import {AnyAction, Reducer} from "redux";

import {HasMessage} from "./ChallengeDetails";
import {ChangeEvent} from "react";
import {AppThunkAction} from "./index";
import {generateErrorMessage, hasContent} from "../RestHelper";

export interface UploadEffortGpxState {
    selectedFile?: File | null,
    athleteId?: string,
    uploading?: boolean,
    uploaded?: boolean,
    errorMessage?: string,
}

export enum UploadEffortGpxActions {
    EffortGpxFileSelected = 'EFFORT_GPX_FILE_SELECTED',
    EffortGpxAthleteIdChanged = 'EFFORT_GPX_ATHLETE_ID_CHANGED',
    EffortGpxFileUploadCanceled = 'EFFORT_GPX_UPLOAD_CANCELED',
    EffortGpxFileUploadStarted = 'EFFORT_GPX_UPLOAD_STARTED',
    EffortGpxFileUploaded = 'EFFORT_GPX_UPLOAD_DONE',
    ServerRequestError = 'ERROR_FETCHING_DATA'
}

export interface EffortGpxFileSelected {
    type: UploadEffortGpxActions.EffortGpxFileSelected,
    selectedFile: File
}

export interface EffortGpxAthleteIdChanged {
    type: UploadEffortGpxActions.EffortGpxAthleteIdChanged,
    athleteId: string
}

export interface EffortGpxFileUploadCanceled {
    type: UploadEffortGpxActions.EffortGpxFileUploadCanceled
}

export interface EffortGpxFileUploadStarted {
    type: UploadEffortGpxActions.EffortGpxFileUploadStarted
}

export interface EffortGpxFileUploaded {
    type: UploadEffortGpxActions.EffortGpxFileUploaded
}

export interface ServerRequestError extends HasMessage {
    type: UploadEffortGpxActions.ServerRequestError
}

export const actionCreators = {
    onAthleteIdChanged: (event: ChangeEvent<HTMLInputElement>): AppThunkAction<KnownAction> => (dispatch, getState) => {
        console.log(`athlete id changing: ${event.target.value}`);
        dispatch({
            type: UploadEffortGpxActions.EffortGpxAthleteIdChanged,
            athleteId: event.target.value
        });
    },
    onFileSelected: (event: ChangeEvent<HTMLInputElement>): AppThunkAction<KnownAction> => (dispatch, getState) => {
        if (event.target.files && event.target.files.length === 1) {
            dispatch({
                type: UploadEffortGpxActions.EffortGpxFileSelected,
                selectedFile: event.target.files[0]
            });
        }
    },
    uploadFile: (): AppThunkAction<KnownAction> => (dispatch, getState) => {
        let appState = getState();

        if (appState.uploadEffortGpx?.selectedFile &&
            appState.challengeDetails?.selectedChallengeName) {

            dispatch({
                type: UploadEffortGpxActions.EffortGpxFileUploadStarted
            });

            let formData = new FormData();
            formData.append('gpxFile', appState.uploadEffortGpx.selectedFile);

            console.log(`upload gpx for: ${appState.uploadEffortGpx.athleteId}`)
            fetch(
                `api/challenges/${appState.challengeDetails.selectedChallengeName}/upload_activity?athlete=${appState.uploadEffortGpx.athleteId}`,
                {method: 'POST', credentials: 'same-origin', body: formData})
                .then(async response => {
                    if (response.ok) {
                        dispatch({
                            type: UploadEffortGpxActions.EffortGpxFileUploaded
                        });
                    } else {
                        let detail: string;
                        if (hasContent(response)) {
                            detail = await response.text();
                        } else {
                            detail = 'Unknown';
                        }

                        console.error(`Error uploading GPX data. Status: ${response.status}, Status Description: ${response.statusText}, Detail: ${detail}`);

                        dispatch({
                            type: UploadEffortGpxActions.ServerRequestError,
                            message: generateErrorMessage('registration status', response.status, response.statusText, detail)
                        });
                    }
                });
        } else {
            console.log('Unable to process uploadFile action. selectedFile is not available in app state.');
        }
    },
    cancelUpload: (): AppThunkAction<KnownAction> => (dispatch, getState) => {
        dispatch({
            type: UploadEffortGpxActions.EffortGpxFileUploadCanceled
        });
    }
};

type KnownAction =
    EffortGpxFileSelected |
    EffortGpxAthleteIdChanged |
    EffortGpxFileUploadCanceled |
    EffortGpxFileUploadStarted |
    EffortGpxFileUploaded |
    ServerRequestError;

const initialState: UploadEffortGpxState = {};

export const reducer: Reducer<UploadEffortGpxState> =
    (state: UploadEffortGpxState | undefined, incomingAction: AnyAction) => {
        state = state || initialState;

        const action = incomingAction as KnownAction;

        switch (action.type) {
            case UploadEffortGpxActions.EffortGpxFileSelected:
                return {
                    ...state,
                    selectedFile: (action as EffortGpxFileSelected).selectedFile,
                };

            case UploadEffortGpxActions.EffortGpxAthleteIdChanged:
                console.log(`athlete id changed: ${(action as EffortGpxAthleteIdChanged).athleteId}`);
                return {
                    ...state,
                    athleteId: (action as EffortGpxAthleteIdChanged).athleteId
                }

            case UploadEffortGpxActions.EffortGpxFileUploadCanceled:
                return {
                    ...state,
                    selectedFile: null
                };

            case UploadEffortGpxActions.EffortGpxFileUploadStarted:
                return {
                    ...state,
                    uploading: true
                };

            case UploadEffortGpxActions.EffortGpxFileUploaded:
                return {
                    ...state,
                    uploading: false,
                    uploaded: true
                };

            case UploadEffortGpxActions.ServerRequestError:
                return {
                    ...state,
                    errorMessage: (action as HasMessage).message,
                    selectedFile: null,
                    uploading: false
                };
        }

        return state;
    };
