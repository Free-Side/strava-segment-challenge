import * as React from 'react';
import {connect} from "react-redux";
import {ApplicationState} from "../store";
import * as ChallengeDetails from "../store/ChallengeDetails";


type JoinButtonProps =
    {joinChallenge: () => void}

class JoinButton extends React.PureComponent<JoinButtonProps> {
    public render() {
        return (<button className="join-button" onClick={() => this.props.joinChallenge()}>Join Challenge</button>);
    }
}
export default connect((state: ApplicationState) => ({}), ChallengeDetails.actionCreators)(
    JoinButton
);
