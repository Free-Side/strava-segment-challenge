import * as React from 'react';
import { ChangeEvent } from 'react';
import { connect, Matching } from 'react-redux';
import { Redirect, RouteComponentProps } from 'react-router';
import { Link } from 'react-router-dom';
import parse from 'html-react-parser';
import moment from 'moment';

import EffortList from './EffortList';
import UploadChallengeGpx from './UploadChallengeGpx';
import NoEffortList from './NoEffortList';
import UploadEffortGpx from './UploadEffortGpx';
import { ApplicationState } from '../store';
import { Challenge } from '../store/ChallengeList';
import * as ChallengeDetailStore from '../store/ChallengeDetails'
import * as ChallengeListStore from '../store/ChallengeList'
import { LoginState } from '../store/Login';
import { Category } from '../store/ChallengeDetails';
import { Modal } from '../shared/Modal';
import { onEnterKey } from '../shared/EventHelpers';
import { IQueryParamsProps, withQueryParams } from '../shared/WithQueryParams';

type ChallengeDetailsProps =
    ChallengeDetailStore.ChallengeDetailsState &
    ChallengeListStore.ChallengeListState &
    { login?: LoginState } &
    {
        onSelectedChallengeChanged: (selectedChallenge: string) => void,
        refreshRegistrationStatus: (selectedChallenge: string) => void,
        selectedCategoryChanged: (selectedCategory: Category) => ChallengeDetailStore.SelectedCategoryChanged,
        joinChallenge: (inviteCode?: string) => void,
        cancelJoin: () => void,
        inviteCodeChanged: (inviteCode: string) => void,
    } &
    RouteComponentProps<{ challengeName: string }> &
    IQueryParamsProps;

type ChallengeDetailsState = {
    bestEffort?: number,
    byCategory?: boolean
};

function relativeUrl(location: Location) {
    return [location.pathname, location.search, location.hash].join('');
}

class ChallengeDetails extends React.PureComponent<Matching<ChallengeDetailsProps, ChallengeDetailsProps>, ChallengeDetailsState> {
    constructor(props: ChallengeDetailsProps) {
        super(props);

        this.state = {};
    }

    public componentDidMount() {
        this.props.onSelectedChallengeChanged(this.props.match.params.challengeName);

        // Only use the query parameter if we don't have this in our props yet.
        let inviteCode = this.props.queryParams.get('inviteCode') ?? undefined;
        if (!this.props.inviteCode && inviteCode) {
            this.props.inviteCodeChanged(inviteCode);
        }

        if (this.props.login?.loggedInUser && this.props.isAthleteRegistered === undefined && this.props.selectedChallengeName) {
            // console.log('refresh registration');
            this.props.refreshRegistrationStatus(this.props.selectedChallengeName);
        } /* else {
            console.log(JSON.stringify({
                loggedInUser: this.props.login?.loggedInUser?.sub,
                isAthleteRegistered: this.props.isAthleteRegistered,
                selectedChallengeName: this.props.selectedChallengeName
            }))
        } */
    }

    public componentDidUpdate() {
        this.props.onSelectedChallengeChanged(this.props.match.params.challengeName);

        if (this.props.allEfforts && this.props.login && this.props.login.loggedInUser) {
            const loggedInUser = this.props.login.loggedInUser;
            this.setState({
                bestEffort: this.props.allEfforts.filter(e => e.athleteId === Number(loggedInUser.sub))[0]?.id
            })
        }
    }

    public render() {
        if (this.props.inviteCode && !this.props.login?.loggedInUser) {
            // If a user is trying to register, but isn't logged in, suggest that the sign up
            return (
                <Redirect to={`/signup?returnUrl=${encodeURIComponent(relativeUrl(this.props.location))}`} />
            );
        }

        return (
            <React.Fragment>
                {this.props.requestError &&
                <span className="error-message">{this.props.requestError}</span>}
                {this.props.currentChallenge ?
                    this.renderChallengeDetails() :
                    (this.props.challenges ? ChallengeDetails.renderNotFound() : ChallengeDetails.renderLoadingIndicator())}
            </React.Fragment>
        );
    }

    private renderChallengeDetails() {
        return this.props.currentChallenge && (
            <div>
                <div id="challenge_title">
                    <h2><a id="strava_segment_link" href={`https://www.strava.com/segments/${this.props.currentChallenge?.segmentId}`} target="_blank"
                           title="View Segment on Strava">{this.props.currentChallenge.displayName}</a></h2>
                    {(this.props.isAthleteRegistered === false) &&
                    <button type="button"
                            className="join-button"
                            onClick={() => this.props.joinChallenge(this.props.inviteCode)}>
                        Join Challenge
                    </button>}
                    {this.state.bestEffort ?
                        <Link to={({ pathname: this.props.location.pathname, hash: `effort_${this.state.bestEffort}` })}
                              className="effort-link">
                            Your Effort
                        </Link> :
                        (this.props.isAthleteRegistered === true &&
                            <span className="joined-successfully">You have joined. Check back later for your efforts.</span>)}
                </div>
                <ul id="challenge_details">
                    <li><span className="challenge-detail-label">Start Date/Time:</span> {moment(this.props.currentChallenge.startDate).format('MMMM Do YYYY, h:mm a')}</li>
                    {(this.props.currentChallenge.startDate.getDate() !== this.props.currentChallenge.endDate.getDate() ||
                        this.props.currentChallenge.startDate.getMonth() !== this.props.currentChallenge.endDate.getMonth()) ?
                        <li><span className="challenge-detail-label">End Date/Time:</span> {moment(this.props.currentChallenge.endDate).format('MMMM Do YYYY, h:mm a')}</li> :
                        <li><span className="challenge-detail-label">End Time:</span> {moment(this.props.currentChallenge.endDate).format('h:mm a')}</li>}
                </ul>
                {this.props.currentChallenge.hasRouteMap &&
                <a href={`https://www.strava.com/segments/${this.props.currentChallenge?.segmentId}`}
                   target="_blank">
                    <img src={`/api/challenges/${this.props.currentChallenge.name}/route_map`} alt="A map of the segment route."
                         className="route-map-image" />
                </a>}
                <p>{parse(this.props.currentChallenge.description)}</p>
                <div className="main-table-container">
                    <h3>
                        {!this.state.byCategory ?
                            <span className="selected-results-display">By Time</span> :
                            <a onClick={() => this.displayResultsByTime()} className="alternate-results-display clickable">By Time</a>}
                        <span> | </span>
                        {this.state.byCategory ?
                            <span className="selected-results-display">By Category</span> :
                            <a onClick={() => this.displayResultsByCategory()} className="alternate-results-display clickable">By Category</a>}
                    </h3>
                    <div className="flex-row row">
                        <EffortList byCategory={!!this.state.byCategory} selectedCategory={null} />
                        {/*<div className="side-panel">*/}
                        {/*    <CategorySelector />*/}
                        {/*</div>*/}
                    </div>
                    <div className="row">
                        <NoEffortList selectedCategory={this.props.selectedCategory} />
                    </div>
                </div>
                {this.props.login?.loggedInUser?.user_data.is_admin && <UploadChallengeGpx selectedCategory={this.props.selectedCategory} />}
                {this.props.isAthleteRegistered &&
                this.props.currentChallenge.startDate < new Date() &&
                <UploadEffortGpx selectedCategory={this.props.selectedCategory} />}

                <Modal open={this.props.waitingForInviteCode === true} closeModal={() => this.props.cancelJoin()}>
                    <div className="invite-code-dialog">
                        <label>Enter Invite Code:
                            <input type="text"
                                   autoFocus={true}
                                   value={this.props.inviteCode}
                                   onChange={(e: ChangeEvent<HTMLInputElement>) => this.props.inviteCodeChanged(e.target.value)}
                                   onKeyPress={onEnterKey(() => this.props.joinChallenge(this.props.inviteCode))} />
                        </label>
                        <p className="form-field-description">
                            If you do not yet have an invite code, you may need to <a href={this.props.currentChallenge.registrationLink} target="_blank">register
                            for the event</a> or contact the challenge organizer.
                        </p>
                        {this.props.registrationError &&
                        <p className="form-field-description error">
                            {this.props.registrationError}
                        </p>}
                        <div className="dialog-buttons">
                            <button type="button"
                                    className="cancel-button"
                                    disabled={this.props.registering}
                                    onClick={() => this.props.cancelJoin()}>
                                Cancel
                            </button>
                            <button type="button"
                                    className="join-button"
                                    disabled={this.props.registering}
                                    onClick={() => this.props.joinChallenge(this.props.inviteCode)}>
                                Join Challenge
                            </button>
                        </div>
                    </div>
                </Modal>
            </div>
        );
    }

    private displayResultsByCategory() {
        this.setState({ byCategory: true });
    }

    private displayResultsByTime() {
        this.setState({ byCategory: false });
    }

    private static renderLoadingIndicator() {
        return (
            <div className="loading-indicator">Loading ...</div>
        )
    }

    private static renderNotFound() {
        return (
            <div>
                <span className="error-message">The Challenge you are looking for was not found.</span>
            </div>
        )
    }

    private static getSelectedChallenge(challenges: Challenge[], challengeName: string): Challenge | undefined {
        return challenges.filter(c => c.name === challengeName)[0];
    }
}

export default connect(
    (state: ApplicationState) => ({ ...state.challengeList, ...state.challengeDetails, login: state.login }),
    ChallengeDetailStore.actionCreators
)(withQueryParams(ChallengeDetails));
