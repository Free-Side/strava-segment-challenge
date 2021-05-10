import * as React from 'react';
import { connect, Matching } from "react-redux";
import moment from "moment";
import * as ChallengeDetailsStore from "../store/ChallengeDetails";
import { ApplicationState } from "../store";
import { ChallengeType } from "../store/ChallengeList";
import { LoginState } from "../store/Login";

type EffortListProps =
    ChallengeDetailsStore.ChallengeDetailsState &
    {
        byCategory: boolean,
        login?: LoginState
    };

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
                    this.renderEffortListTable(this.props.allEfforts, this.props.currentChallenge?.type, this.props.byCategory) :
                    EffortList.renderLoadingIndicator()}
            </React.Fragment>
        );
    }

    private renderEffortListTable(
        efforts: ChallengeDetailsStore.Effort[],
        challengeType: ChallengeType | undefined,
        byCategory: boolean) {
        // TODO filter by category
        const showLapCount = challengeType === ChallengeType.MostLaps;

        let renderCategoryResults = (categoryName: string, categoryEfforts: ChallengeDetailsStore.Effort[]) => {
            const columns = showLapCount ? 4 : 3;
            return (
                <tbody key={categoryName}>
                <tr className="table-header-row">
                    <th className="category-header-row" colSpan={columns}>{categoryName}</th>
                </tr>
                <tr className="table-header-row">
                    <th>Place</th>
                    <th>Athlete</th>
                    {showLapCount && <th>Lap Count</th>}
                    <th>Time</th>
                </tr>
                {categoryEfforts.map((effort, ix) =>
                    <tr id={`effort_${effort.id}`} key={effort.id}>
                        <td>{ix + 1}</td>
                        <td>{effort.athleteName}{this.props.login?.loggedInUser?.user_data.is_admin && (` (${effort.athleteId})`)}</td>
                        {showLapCount && <td>{effort.lapCount}</td>}
                        <td>{toTimeFormat(moment.duration(effort.elapsedTime, 'seconds'))}</td>
                    </tr>
                )}
                </tbody>
            );
        }

        if (byCategory) {
            let resultsByCategory: any = {};
            for (const effort of efforts) {
                const cat = this.getCategory(effort.athleteAge, effort.athleteGender, effort.specialCategoryId);
                if (!resultsByCategory[cat]) {
                    resultsByCategory[cat] = [effort];
                } else {
                    resultsByCategory[cat].push(effort);
                }
            }
            return (
                <table className="main-table table-striped">
                    {Object.keys(resultsByCategory).map(cat => renderCategoryResults(cat, resultsByCategory[cat]))}
                </table>
            );
        } else {
            const showCategory = !(this.props.selectedCategory?.maximumAge && this.props.selectedCategory?.gender);
            return (
                <table className="main-table table-striped">
                    <thead>
                    <tr>
                        <th>Athlete</th>
                        {showCategory && <th>Category</th>}
                        {showLapCount && <th>Lap Count</th>}
                        <th>Time</th>
                    </tr>
                    </thead>
                    <tbody>
                    {efforts.map((effort: ChallengeDetailsStore.Effort) =>
                        <tr id={`effort_${effort.id}`} key={effort.id}>
                            <td>{effort.athleteName}{this.props.login?.loggedInUser?.user_data.is_admin && (` (${effort.athleteId})`)}</td>
                            {showCategory && <td>{this.getCategory(effort.athleteAge, effort.athleteGender, effort.specialCategoryId)}</td>}
                            {showLapCount && <td>{effort.lapCount}</td>}
                            <td className={effort.isKOM ? 'kom' : ''}>{toTimeFormat(moment.duration(effort.elapsedTime, 'seconds'))}</td>
                        </tr>
                    )}
                    </tbody>
                </table>
            );
        }
    }

    private getCategory(athleteAge: number, athleteGender: string, specialCategoryId: number | null): string {
        if (this.props.ageGroups && this.props.specialCategories) {
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

            let category;
            if (specialCategoryId != null) {
                const cat = this.props.specialCategories.filter(c => c.specialCategoryId === specialCategoryId)[0];
                if (cat) {
                    category = cat.categoryName;
                }
            } else if (this.props.ageGroups) {
                const ageGroup = this.props.ageGroups.filter(a => a.maximumAge >= athleteAge)[0];
                if (ageGroup) {
                    category = ageGroup.description ;
                }
            }

            if (category) {
                return `${gender}, ${category}`
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
    (state: ApplicationState) => ({...state.challengeDetails, login: state.login})
)(EffortList);
