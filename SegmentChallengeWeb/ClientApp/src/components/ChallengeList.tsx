import * as React from 'react';
import {connect, Matching} from 'react-redux';
import {Link} from "react-router-dom";
import moment from "moment";
import * as ChallengeListStore from '../store/ChallengeList'
import {ApplicationState} from "../store";

type ChallengeListProps =
    ChallengeListStore.ChallengeListState &
    { requestChallengeList: () => void };

class ChallengeList extends React.PureComponent<Matching<ChallengeListProps, ChallengeListProps>> {
    public componentDidMount() {
        this.props.requestChallengeList();
    }

    public render() {
        return (
            <React.Fragment>
                {this.props.requestError &&
                    <span className="error-message">{this.props.requestError}</span>}
                {this.props.challenges ?
                    ChallengeList.renderChallengeListTable(this.props.challenges) :
                    ChallengeList.renderLoadingIndicator()}
            </React.Fragment>
        );
    }

    private static renderChallengeListTable(challenges: ChallengeListStore.Challenge[]) {
        return (
            <table className='main-table'>
                <thead>
                    <tr>
                        <td>Segment</td>
                        <td>Start Date</td>
                        <td>End Date</td>
                    </tr>
                </thead>
                <tbody>
                {challenges.map((challenge: ChallengeListStore.Challenge) =>
                    <tr key={challenge.name} className={(challenge.endDate > new Date()) ? 'active-challenge' : 'inactive-challenge'}>
                        <td><Link to={`/challenge/${challenge.name}`}>{challenge.displayName}</Link></td>
                        <td title={moment(challenge.startDate).format('MMMM Do YYYY, h:mm:ss a')}>{moment(challenge.startDate).fromNow()}</td>
                        <td title={moment(challenge.endDate).format('MMMM Do YYYY, h:mm:ss a')}>{moment(challenge.endDate).fromNow()}</td>
                    </tr>
                )}
                </tbody>
            </table>
        );
    }

    private static renderLoadingIndicator() {
        return (
            <div className="loading-indicator">Loading ...</div>
        )
    }
}

export default connect(
  (state: ApplicationState) => state.challengeList,
  ChallengeListStore.actionCreators
)(ChallengeList);
