import * as React from 'react';
import {connect, Matching} from "react-redux";
import * as ChallengeDetailsStore from "../store/ChallengeDetails";
import moment from "moment";
import {ApplicationState} from "../store";
import {ChallengeType} from "../store/ChallengeList";

type EffortListProps =
    ChallengeDetailsStore.ChallengeDetailsState;

function toTimeFormat(duration: moment.Duration) {
    let str = ('0' + duration.seconds()).slice(-2);

    if (duration.asMinutes() >= 1.0) {
        str = `${('0' + duration.minutes()).slice(-2)}:${str}`;

        if (duration.asHours() >= 1.0) {
            str = `${('0' + duration.hours()).slice(-2)}:${str}`;

            if (duration.asDays() >= 1.0) {
                str = `${Math.floor(duration.asDays())} days ${str}`;
            }
        }
    } else {
        str += ' seconds';
    }

    return str;
}

class EffortList extends React.PureComponent<Matching<EffortListProps, EffortListProps>> {
    public render() {
        return (
            <React.Fragment>
                {this.props.errorMessage &&
                    <span className="error-message">{this.props.errorMessage}</span>}
                {this.props.allEfforts ?
                    this.renderEffortListTable(this.props.currentChallenge?.type, this.props.allEfforts) :
                    EffortList.renderLoadingIndicator()}
            </React.Fragment>
        );
    }

    private renderEffortListTable(challengeType: ChallengeType | undefined, efforts: ChallengeDetailsStore.Effort[]) {
        // TODO filter by category
        const showCategory = !(this.props.selectedCategory.maximumAge && this.props.selectedCategory.gender);
        const showLapCount = challengeType === ChallengeType.MostLaps;
        return (
            <table className='main-table table-striped'>
                <thead>
                    <tr>
                        <td>Athlete</td>
                        {showCategory && <td>Category</td>}
                        {showLapCount && <td>Lap Count</td>}
                        <td>Time</td>
                    </tr>
                </thead>
                <tbody>
                {efforts.map((effort: ChallengeDetailsStore.Effort) =>
                    <tr id={`effort_${effort.id}`} key={effort.id}>
                        <td>{effort.athleteName}</td>
                        {showCategory && <td>{this.getCategory(effort.athleteAge, effort.athleteGender)}</td>}
                        {showLapCount && <td>{effort.lapCount}</td>}
                        <td className={effort.isKOM ? 'kom' : ''}>{toTimeFormat(moment.duration(effort.elapsedTime, 'seconds'))}</td>
                    </tr>
                )}
                </tbody>
            </table>
        );
    }

    private getCategory(athleteAge: number, athleteGender: string): string {
        if (this.props.ageGroups) {
            let gender = 'Other';
            switch (athleteGender) {
                case 'm':
                case 'M':
                    gender = 'Men';
                    break;
                case 'f':
                case 'F':
                    gender = 'Women';
                    break;
            }

            const ageGroup = this.props.ageGroups.filter(a => a.maximumAge > athleteAge)[0];
            if (ageGroup) {
                return `${gender}, ${ageGroup.description}`;
            } else {
                return gender;
            }
        } else {
            return 'Unknown';
        }
    }

    private static renderLoadingIndicator() {
        return (
            <div className="loading-indicator">Loading ...</div>
        )
    }
}

export default connect(
  (state: ApplicationState) => state.challengeDetails
)(EffortList);
