import {Action, AnyAction, Reducer} from 'redux';

export interface ConfigurationState {
  siteTitle: string,
  siteLogo?: string,
  siteFooter?: string,
  apiBaseUrl: string
}

export interface SetConfigurationAction {
  type: 'SET_CONFIGURATION',
  config: ConfigurationState
}

function isSetConfigurationAction(action: Action): action is SetConfigurationAction {
  return action.type === 'SET_CONFIGURATION';
}

const initialState = { siteTitle: 'Strava Segment Challenge', apiBaseUrl: '' } as ConfigurationState;

export const reducer: Reducer<ConfigurationState> = (state: ConfigurationState | undefined, action: AnyAction) => {
  state = state || initialState;

  if (isSetConfigurationAction(action)) {
    return { ...state, config: action.config };
  }

  return state;
};
