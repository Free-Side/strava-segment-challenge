import * as Configuration from './Configuration';
import * as Login from './Login';
import * as ChallengeList from "./ChallengeList";
import * as ChallengeDetails from "./ChallengeDetails";

// The top-level state object
export interface ApplicationState {
    login?: Login.LoginState;
    config: Configuration.ConfigurationState;
    challengeList?: ChallengeList.ChallengeListState;
    challengeDetails?: ChallengeDetails.ChallengeDetailsState;
}

export const reducers = {
    login: Login.reducer,
    config: Configuration.reducer,
    challengeList: ChallengeList.reducer,
    challengeDetails: ChallengeDetails.reducer
};

// This type can be used as a hint on action creators so that its 'dispatch' and 'getState' params are
// correctly typed to match your store.
export interface AppThunkAction<TAction> {
    (dispatch: (action: TAction) => void, getState: () => ApplicationState): void;
}
