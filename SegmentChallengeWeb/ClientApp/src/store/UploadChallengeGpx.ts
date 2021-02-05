import {AnyAction, Reducer} from "redux";

import {HasMessage} from "./ChallengeDetails";
import {ChangeEvent} from "react";
import {AppThunkAction} from "./index";
import {generateErrorMessage, hasContent} from "../RestHelper";

export interface UploadChallengeGpxState {
    selectedFile?: File | null,
    uploading?: boolean,
    uploaded?: boolean,
    errorMessage?: string,
}

export enum UploadChallengeGpxActions {
    ChallengeGpxFileSelected = 'CHALLENGE_GPX_FILE_SELECTED',
    ChallengeGpxFileUploadCanceled = 'CHALLENGE_GPX_UPLOAD_CANCELED',
    ChallengeGpxFileUploadStarted = 'CHALLENGE_GPX_UPLOAD_STARTED',
    ChallengeGpxFileUploaded = 'CHALLENGE_GPX_UPLOAD_DONE',
    ServerRequestError = 'ERROR_FETCHING_DATA'
}

export interface ChallengeGpxFileSelected {
    type: UploadChallengeGpxActions.ChallengeGpxFileSelected,
    selectedFile: File
}

export interface ChallengeGpxFileUploadCanceled {
    type: UploadChallengeGpxActions.ChallengeGpxFileUploadCanceled
}

export interface ChallengeGpxFileUploadStarted {
    type: UploadChallengeGpxActions.ChallengeGpxFileUploadStarted
}

export interface ChallengeGpxFileUploaded {
    type: UploadChallengeGpxActions.ChallengeGpxFileUploaded
}

export interface ServerRequestError extends HasMessage {
    type: UploadChallengeGpxActions.ServerRequestError
}

export const actionCreators = {
    onFileSelected: (event: ChangeEvent<HTMLInputElement>): AppThunkAction<KnownAction> => (dispatch, getState) => {
        if (event.target.files && event.target.files.length === 1) {
            dispatch({
                type: UploadChallengeGpxActions.ChallengeGpxFileSelected,
                selectedFile: event.target.files[0]
            });
        }
    },
    uploadFile: (): AppThunkAction<KnownAction> => (dispatch, getState) => {
        let appState = getState();

        if (appState.uploadChallengeGpx?.selectedFile &&
            appState.challengeDetails?.selectedChallengeName) {

            dispatch({
                type: UploadChallengeGpxActions.ChallengeGpxFileUploadStarted
            });

            let formData = new FormData();
            formData.append('gpxFile', appState.uploadChallengeGpx.selectedFile);

            fetch(
                `api/challenges/${appState.challengeDetails.selectedChallengeName}/set_gpx`,
                {method: 'POST', credentials: 'same-origin', body: formData})
                .then(async response => {
                    if (response.ok) {
                        dispatch({
                            type: UploadChallengeGpxActions.ChallengeGpxFileUploaded
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
                            type: UploadChallengeGpxActions.ServerRequestError,
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
            type: UploadChallengeGpxActions.ChallengeGpxFileUploadCanceled
        });
    }
};

type KnownAction =
    ChallengeGpxFileSelected |
    ChallengeGpxFileUploadCanceled |
    ChallengeGpxFileUploadStarted |
    ChallengeGpxFileUploaded |
    ServerRequestError;

const initialState: UploadChallengeGpxState = {};

export const reducer: Reducer<UploadChallengeGpxState> =
    (state: UploadChallengeGpxState | undefined, incomingAction: AnyAction) => {
        state = state || initialState;

        const action = incomingAction as KnownAction;

        switch (action.type) {
            case UploadChallengeGpxActions.ChallengeGpxFileSelected:
                return {
                    ...state,
                    selectedFile: (action as ChallengeGpxFileSelected).selectedFile,
                };

            case UploadChallengeGpxActions.ChallengeGpxFileUploadCanceled:
                return {
                    ...state,
                    selectedFile: null
                };

            case UploadChallengeGpxActions.ChallengeGpxFileUploadStarted:
                return {
                    ...state,
                    uploading: true
                };

            case UploadChallengeGpxActions.ChallengeGpxFileUploaded:
                return {
                    ...state,
                    uploading: false,
                    uploaded: true
                };

            case UploadChallengeGpxActions.ServerRequestError:
                return {
                    ...state,
                    errorMessage: (action as HasMessage).message,
                    selectedFile: null,
                    uploading: false
                };
        }

        return state;
    };
